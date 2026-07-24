using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.ConfigTuning;

internal sealed class ParameterEditSession : IParameterEditSession
{
    private const double EqualityTolerance = 0.0001;
    private readonly object sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterService parameterService;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly TimeSpan readbackTimeout;
    private readonly ILogger<ParameterEditSession> logger;
    private readonly Dictionary<string, ParameterEditField> fields = new(StringComparer.Ordinal);
    private readonly List<string> fieldOrder = [];
    private readonly SemaphoreSlim applyGate = new(1, 1);
    private string? invalidReason;
    private bool disposed;

    public ParameterEditSession(
        ParameterEditScope scope,
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterService parameterService,
        IVehicleParameterMetadataService metadataService,
        IOptions<ParameterEditSessionOptions> options,
        ILogger<ParameterEditSession> logger)
    {
        Scope = scope;
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.parameterService = parameterService;
        this.metadataService = metadataService;
        readbackTimeout = options.Value.ReadbackTimeout > TimeSpan.Zero ? options.Value.ReadbackTimeout : TimeSpan.FromSeconds(3);
        this.logger = logger;
        parameterRegistry.Changed += OnParameterChanged;
    }

    /// <inheritdoc />
    public ParameterEditScope Scope { get; }

    /// <inheritdoc />
    public VehicleId VehicleId => Scope.VehicleId;

    /// <inheritdoc />
    public IReadOnlyList<ParameterEditField> Fields
    {
        get
        {
            lock (sync)
            {
                return fieldOrder.Select(name => fields[name]).ToArray();
            }
        }
    }

    /// <inheritdoc />
    public bool IsDirty
    {
        get
        {
            lock (sync)
            {
                return fields.Values.Any(item => item.IsModified);
            }
        }
    }

    /// <inheritdoc />
    public bool IsValid
    {
        get
        {
            lock (sync)
            {
                return !disposed && invalidReason is null && ScopeMatchesActiveVehicle();
            }
        }
    }

    /// <inheritdoc />
    public string? InvalidReason
    {
        get
        {
            lock (sync)
            {
                return invalidReason;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public async Task LoadAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default)
    {
        EnsureValid();
        var parameters = parameterRegistry.GetAllParameters(VehicleId);
        var selectedNames = names is null
            ? parameters.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray()
            : names.Where(parameters.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
        await LoadCoreAsync(parameters, selectedNames, names is null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LoadDefinitionsAsync(IReadOnlyList<ParameterFieldDefinition> definitions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        EnsureValid();
        var parameters = parameterRegistry.GetAllParameters(VehicleId);
        var names = definitions
            .Select(definition => definition.Resolve(parameters))
            .Where(name => name is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await LoadCoreAsync(parameters, names, false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ParameterEditField? GetField(string name)
    {
        lock (sync)
        {
            return fields.GetValueOrDefault(name);
        }
    }

    /// <inheritdoc />
    public bool TrySetPending(string name, double value, out string? error)
    {
        ParameterEditField updated;
        lock (sync)
        {
            if (!fields.TryGetValue(name, out var field))
            {
                error = $"Parameter {name} is not loaded in this editing session.";
                return false;
            }

            error = Validate(field, value);
            updated = field with { PendingValue = value, ValidationError = error, WriteStatus = NearlyEqual(value, field.LiveValue) ? ParameterEditWriteStatus.Unchanged : ParameterEditWriteStatus.Pending, WriteMessage = null };
            fields[name] = updated;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return error is null;
    }

    /// <inheritdoc />
    public void Revert(string name)
    {
        var changed = false;
        lock (sync)
        {
            if (fields.TryGetValue(name, out var field))
            {
                fields[name] = field with { PendingValue = field.LiveValue, ValidationError = null, WriteStatus = ParameterEditWriteStatus.Unchanged, WriteMessage = null };
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void RevertAll()
    {
        lock (sync)
        {
            foreach (var name in fieldOrder)
            {
                var field = fields[name];
                fields[name] = field with { PendingValue = field.LiveValue, ValidationError = null, WriteStatus = ParameterEditWriteStatus.Unchanged, WriteMessage = null };
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task<ParameterApplyReport> ApplyAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default)
    {
        await applyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var targets = GetApplyTargets(names);
            logger.LogInformation("Applying {Count} parameter edits to {VehicleId}.", targets.Count, VehicleId);
            var results = new List<ParameterWriteResult>(targets.Count);
            var rebootRequired = false;
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);

            for (var index = 0; index < targets.Count; index++)
            {
                var name = targets[index];
                if (!TryEnsureValid(out var scopeError))
                {
                    Invalidate(scopeError);
                    AppendSkipped(targets, index, results, scopeError);
                    break;
                }

                var field = GetField(name);
                if (field is null)
                {
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Skipped, "The parameter is not loaded in this session."));
                    continue;
                }

                if (!field.IsModified)
                {
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Unchanged, "The live value already matches the pending value."));
                    continue;
                }

                var validationError = Validate(field, field.PendingValue);
                if (validationError is not null)
                {
                    SetWriteState(name, ParameterEditWriteStatus.Failed, validationError, validationError);
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.ValidationFailed, validationError));
                    continue;
                }

                SetWriteState(name, ParameterEditWriteStatus.Applying, "Waiting for vehicle readback.", null);
                try
                {
                    var write = await WriteAndConfirmAsync(field, connectionCancellation.Token).ConfigureAwait(false);
                    if (!write.Sent)
                    {
                        const string message = "The vehicle rejected or could not send the parameter write.";
                        SetWriteState(name, ParameterEditWriteStatus.Failed, message, null);
                        results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.WriteFailed, message));
                        continue;
                    }

                    if (write.Readback is null)
                    {
                        const string message = "The write was sent but matching live readback was not received before the timeout.";
                        SetWriteState(name, ParameterEditWriteStatus.Failed, message, null);
                        results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.ReadbackFailed, message));
                        continue;
                    }

                    ConfirmWrite(name, write.Readback);
                    rebootRequired |= field.Metadata.RebootRequired;
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Confirmed, "Confirmed by live vehicle readback."));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    const string message = "The active vehicle connection changed before the write was confirmed.";
                    Invalidate(message);
                    SetWriteState(name, ParameterEditWriteStatus.Failed, message, null);
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.ReadbackFailed, message));
                    AppendSkipped(targets, index + 1, results, message);
                    break;
                }
            }

            var success = results.All(result => result.Outcome is ParameterWriteOutcome.Confirmed or ParameterWriteOutcome.Unchanged);
            logger.LogInformation(
                "Parameter apply for {VehicleId} completed. Confirmed={ConfirmedCount}, Failed={FailedCount}, RebootRequired={RebootRequired}.",
                VehicleId,
                results.Count(result => result.Outcome == ParameterWriteOutcome.Confirmed),
                results.Count(result => result.Outcome is not ParameterWriteOutcome.Confirmed and not ParameterWriteOutcome.Unchanged),
                rebootRequired);
            return new ParameterApplyReport(success, results, rebootRequired);
        }
        finally
        {
            applyGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default)
    {
        EnsureValid();
        var targets = names is null
            ? Fields.Select(field => field.Name).ToArray()
            : names.Distinct(StringComparer.Ordinal).ToArray();
        logger.LogInformation("Refreshing {Count} edited parameters for {VehicleId}.", targets.Length, VehicleId);
        using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            activeVehicle.ConnectionCancellationToken);
        foreach (var name in targets)
        {
            connectionCancellation.Token.ThrowIfCancellationRequested();
            if (GetField(name) is null)
            {
                continue;
            }

            if (!await parameterService.RequestParameterAsync(VehicleId, name, connectionCancellation.Token).ConfigureAwait(false))
            {
                SetWriteState(name, ParameterEditWriteStatus.Failed, "The refresh request could not be sent.", null);
            }
        }
    }

    public void Invalidate(string reason)
    {
        lock (sync)
        {
            if (invalidReason is not null)
            {
                return;
            }

            invalidReason = reason;
            foreach (var name in fieldOrder.Where(name => fields[name].IsModified))
            {
                fields[name] = fields[name] with { WriteStatus = ParameterEditWriteStatus.Failed, WriteMessage = reason };
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            invalidReason ??= "The parameter editing session was disposed.";
        }

        parameterRegistry.Changed -= OnParameterChanged;
        applyGate.Dispose();
    }

    private async Task LoadCoreAsync(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyList<string> names, bool replace, CancellationToken cancellationToken)
    {
        var metadata = await metadataService.GetAllMetadataAsync(VehicleId, cancellationToken).ConfigureAwait(false);
        lock (sync)
        {
            EnsureValidUnderLock();
            if (replace)
            {
                var removed = fields.Keys.Except(names, StringComparer.Ordinal).ToArray();
                foreach (var name in removed)
                {
                    fields.Remove(name);
                }

                fieldOrder.Clear();
            }

            foreach (var name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!parameters.TryGetValue(name, out var parameter))
                {
                    continue;
                }

                var projectedMetadata = ProjectMetadata(metadata.GetValueOrDefault(name));
                if (fields.TryGetValue(name, out var existing))
                {
                    var pending = existing.IsModified ? existing.PendingValue : parameter.Value;
                    var validation = Validate(existing with { Metadata = projectedMetadata, Type = parameter.Type }, pending);
                    fields[name] = existing with
                    {
                        Type = parameter.Type,
                        LiveValue = parameter.Value,
                        PendingValue = pending,
                        Metadata = projectedMetadata,
                        ValidationError = validation,
                        WriteStatus = NearlyEqual(pending, parameter.Value) ? ParameterEditWriteStatus.Unchanged : ParameterEditWriteStatus.Pending,
                        WriteMessage = null
                    };
                }
                else
                {
                    fields[name] = new ParameterEditField(
                        name,
                        parameter.Type,
                        parameter.Value,
                        parameter.Value,
                        parameter.Value,
                        projectedMetadata,
                        null);
                }

                if (!fieldOrder.Contains(name, StringComparer.Ordinal))
                {
                    fieldOrder.Add(name);
                }
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<string> GetApplyTargets(IReadOnlyList<string>? names)
    {
        lock (sync)
        {
            return names is null
                ? fieldOrder.Where(name => fields[name].IsModified).ToArray()
                : names.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    private async Task<(bool Sent, VehicleParameter? Readback)> WriteAndConfirmAsync(ParameterEditField field, CancellationToken cancellationToken)
    {
        var expected = (float)field.PendingValue;
        var readback = new TaskCompletionSource<VehicleParameter>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == VehicleId && args.Parameter is { } parameter &&
                parameter.Name == field.Name && NearlyEqual(parameter.Value, expected))
            {
                readback.TrySetResult(parameter);
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(VehicleId, field.Name, expected, field.Type, cancellationToken).ConfigureAwait(false))
            {
                return (false, null);
            }

            if (parameterRegistry.GetParameter(VehicleId, field.Name) is { } current && NearlyEqual(current.Value, expected))
            {
                return (true, current);
            }

            await parameterService.RequestParameterAsync(VehicleId, field.Name, cancellationToken).ConfigureAwait(false);
            try
            {
                return (true, await readback.Task.WaitAsync(readbackTimeout, cancellationToken).ConfigureAwait(false));
            }
            catch (TimeoutException)
            {
                return (true, null);
            }
        }
        finally
        {
            parameterRegistry.Changed -= OnChanged;
        }
    }

    private void ConfirmWrite(string name, VehicleParameter readback)
    {
        lock (sync)
        {
            if (!fields.TryGetValue(name, out var field))
            {
                return;
            }

            fields[name] = field with
            {
                Type = readback.Type,
                LiveValue = readback.Value,
                PendingValue = readback.Value,
                ValidationError = null,
                WriteStatus = ParameterEditWriteStatus.Confirmed,
                WriteMessage = "Confirmed by live vehicle readback."
            };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetWriteState(string name, ParameterEditWriteStatus status, string message, string? validationError)
    {
        lock (sync)
        {
            if (!fields.TryGetValue(name, out var field))
            {
                return;
            }

            fields[name] = field with { WriteStatus = status, WriteMessage = message, ValidationError = validationError };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnParameterChanged(object? sender, VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId != VehicleId || args.Parameter is not { } parameter)
        {
            return;
        }

        var changed = false;
        lock (sync)
        {
            if (disposed || !fields.TryGetValue(parameter.Name, out var field))
            {
                return;
            }

            var preservePending = field.IsModified;
            var pending = preservePending ? field.PendingValue : parameter.Value;
            var remainsModified = !NearlyEqual(pending, parameter.Value);
            fields[parameter.Name] = field with
            {
                Type = parameter.Type,
                LiveValue = parameter.Value,
                PendingValue = pending,
                ValidationError = Validate(field with { Type = parameter.Type, LiveValue = parameter.Value }, pending),
                WriteStatus = remainsModified
                    ? field.WriteStatus == ParameterEditWriteStatus.Applying ? ParameterEditWriteStatus.Applying : ParameterEditWriteStatus.Pending
                    : field.WriteStatus == ParameterEditWriteStatus.Applying
                        ? ParameterEditWriteStatus.Applying
                        : ParameterEditWriteStatus.Unchanged,
                WriteMessage = remainsModified ? field.WriteMessage : null
            };
            changed = true;
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool TryEnsureValid(out string error)
    {
        lock (sync)
        {
            if (disposed)
            {
                error = "The parameter editing session was disposed.";
                return false;
            }

            if (invalidReason is not null)
            {
                error = invalidReason;
                return false;
            }

            if (!ScopeMatchesActiveVehicle())
            {
                error = "The active vehicle connection or firmware identity no longer matches this parameter session.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    private void EnsureValid()
    {
        if (!TryEnsureValid(out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private void EnsureValidUnderLock()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ParameterEditSession));
        }

        if (invalidReason is not null || !ScopeMatchesActiveVehicle())
        {
            throw new InvalidOperationException(invalidReason ?? "The active vehicle no longer matches this parameter session.");
        }
    }

    private bool ScopeMatchesActiveVehicle()
    {
        return activeVehicle.IsOnline &&
               activeVehicle.VehicleId == VehicleId &&
               activeVehicle.State?.Identity.Firmware == Scope.FirmwareIdentity;
    }

    private static string? Validate(ParameterEditField field, double value)
    {
        if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
        {
            return "Value must be a finite MAVLink 32-bit parameter value.";
        }

        if (field.Metadata.ReadOnly && !NearlyEqual(value, field.LiveValue))
        {
            return "This parameter is read-only and cannot be modified.";
        }

        if (field.Metadata.Minimum is { } minimum && value < minimum - EqualityTolerance)
        {
            return $"Value must be at least {minimum}.";
        }

        if (field.Metadata.Maximum is { } maximum && value > maximum + EqualityTolerance)
        {
            return $"Value must be at most {maximum}.";
        }

        if (field.Type != MavParamType.Real32 && !NearlyEqual(value, Math.Round(value)))
        {
            return "This parameter requires a whole-number value.";
        }

        var typeError = ValidateTypeRange(field.Type, value);
        if (typeError is not null)
        {
            return typeError;
        }

        if (field.Metadata.Options.Count > 0 && !field.Metadata.Options.Any(option => NearlyEqual(option.Value, value)))
        {
            return "Select one of the values advertised by the vehicle firmware metadata.";
        }

        if (field.Metadata.Bitmask.Count > 0)
        {
            if (value < 0 || !NearlyEqual(value, Math.Round(value)))
            {
                return "A bitmask value must be a non-negative whole number.";
            }

            var selected = checked((ulong)Math.Round(value));
            var allowed = field.Metadata.Bitmask
                .Where(option => option.Bit is >= 0 and < 64)
                .Aggregate(0UL, (mask, option) => mask | (1UL << option.Bit));
            if ((selected & ~allowed) != 0)
            {
                return "The value contains bitmask flags not advertised by the vehicle firmware metadata.";
            }
        }

        if (field.Metadata.Increment is > 0 and var increment && field.Metadata.Options.Count == 0)
        {
            var origin = field.Metadata.Minimum ?? 0;
            var steps = (value - origin) / increment;
            if (!NearlyEqual(steps, Math.Round(steps)))
            {
                return $"Value must use increments of {increment}.";
            }
        }

        return null;
    }

    private static string? ValidateTypeRange(MavParamType type, double value)
    {
        return type switch
        {
            MavParamType.Uint8 when value is < byte.MinValue or > byte.MaxValue => "Value must fit an unsigned 8-bit parameter.",
            MavParamType.Int8 when value is < sbyte.MinValue or > sbyte.MaxValue => "Value must fit a signed 8-bit parameter.",
            MavParamType.Uint16 when value is < ushort.MinValue or > ushort.MaxValue => "Value must fit an unsigned 16-bit parameter.",
            MavParamType.Int16 when value is < short.MinValue or > short.MaxValue => "Value must fit a signed 16-bit parameter.",
            MavParamType.Uint32 when value is < uint.MinValue or > uint.MaxValue => "Value must fit an unsigned 32-bit parameter.",
            MavParamType.Int32 when value is < int.MinValue or > int.MaxValue => "Value must fit a signed 32-bit parameter.",
            var _ => null
        };
    }

    private static ParameterFieldMetadata ProjectMetadata(ParameterMetadata? metadata)
    {
        return metadata is null
            ? ParameterFieldMetadata.Empty
            : new ParameterFieldMetadata(
                metadata.DisplayName,
                metadata.Description,
                metadata.Units,
                metadata.MinValue,
                metadata.MaxValue,
                metadata.IncrementValue,
                metadata.ReadOnly,
                metadata.RebootRequired,
                metadata.GetValueOptions()
                    .OrderBy(option => option.Key)
                    .Select(option => new ParameterValueOption(option.Key, option.Value))
                    .ToArray(),
                metadata.GetBitmaskOptions()
                    .OrderBy(option => option.Key)
                    .Select(option => new ParameterBitOption(option.Key, option.Value))
                    .ToArray())
            {
                UnitText = metadata.UnitText,
                RangeText = metadata.Range,
                ValuesText = metadata.Values,
                BitmaskText = metadata.Bitmask,
                IncrementText = metadata.Increment,
                UserLevel = metadata.UserLevel
            };
    }

    private static void AppendSkipped(
        IReadOnlyList<string> targets,
        int startIndex,
        ICollection<ParameterWriteResult> results,
        string message)
    {
        for (var index = startIndex; index < targets.Count; index++)
        {
            results.Add(new ParameterWriteResult(targets[index], ParameterWriteOutcome.Skipped, message));
        }
    }

    private static bool NearlyEqual(double first, double second)
    {
        var scale = Math.Max(1, Math.Max(Math.Abs(first), Math.Abs(second)));
        return Math.Abs(first - second) <= EqualityTolerance * scale;
    }
}
