using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.Math;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>
/// Represents a session for editing vehicle parameters, allowing for loading, modifying, validating, and applying parameter changes in a controlled manner. 
/// </summary>
public sealed class ParameterEditSession : IParameterEditSession
{
    private readonly Lock sync = new();
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterEditSession"/> class.
    /// </summary>
    /// <param name="scope">The scope of the parameter edit session.</param>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="parameterRegistry">The vehicle parameter registry.</param>
    /// <param name="parameterService">The vehicle parameter service.</param>
    /// <param name="metadataService">The vehicle parameter metadata service.</param>
    /// <param name="options">The options for the parameter edit session.</param>
    /// <param name="logger">The logger for the parameter edit session.</param>
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
        this.logger = logger;
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.parameterService = parameterService;
        this.metadataService = metadataService;
        readbackTimeout = options.Value.ReadbackTimeout > TimeSpan.Zero ? options.Value.ReadbackTimeout : TimeSpan.FromSeconds(3);
        parameterRegistry.Changed += OnParameterChanged;
    }

    /// <inheritdoc />
    public ParameterEditScope Scope
    {
        get;
    }

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
    public Action? Changed
    {
        get; set;
    }

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
            updated = field with
            {
                PendingValue = value,
                ValidationError = error,
                WriteStatus = Equivalent(value, field.LiveValue, field.Metadata) ? ParameterEditWriteStatus.Unchanged : ParameterEditWriteStatus.Pending,
                WriteMessage = null
            };
            fields[name] = updated;
        }

        Changed?.Invoke();
        return error is null;
    }

    /// <inheritdoc />
    public ParameterWritePlan CreateWritePlan(IReadOnlyList<string>? names = null)
    {
        EnsureValid();

        var requestedNames = names?.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        ParameterEditField[] snapshot;
        lock (sync)
        {
            snapshot = fieldOrder
                .Where(name => requestedNames is null || requestedNames.Contains(name))
                .Select(name => fields[name])
                .Where(field => field.IsModified)
                .ToArray();
        }

        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException("There are no modified parameters to write.");
        }

        var blocked = snapshot.FirstOrDefault(field =>
            field.Metadata.ReadOnly || field.ValidationError is not null || Validate(field, field.PendingValue) is not null);
        if (blocked is not null)
        {
            var reason = blocked.Metadata.ReadOnly
                ? "The parameter is read-only."
                : blocked.ValidationError ?? Validate(blocked, blocked.PendingValue);
            throw new InvalidOperationException($"Parameter {blocked.Name} cannot be written: {reason}");
        }

        var entries = snapshot.Select(field => new ParameterWritePlanEntry(
            field.Name,
            field.Metadata.DisplayName ?? field.Name,
            field.LiveValue,
            field.PendingValue,
            field.Metadata.Units,
            field.PendingValue - field.LiveValue,
            field.Metadata.RebootRequired,
            field.Metadata.ReadOnly,
            field.ValidationError)).ToArray();

        logger.LogInformation(
            "Created parameter write plan with {Count} entries for {VehicleId}.",
            entries.Length,
            VehicleId);
        return new ParameterWritePlan(Scope, DateTimeOffset.UtcNow, entries);
    }

    /// <inheritdoc />
    public void Revert(string name)
    {
        var changed = false;
        lock (sync)
        {
            if (fields.TryGetValue(name, out var field))
            {
                fields[name] = field with
                {
                    PendingValue = field.LiveValue,
                    ValidationError = null,
                    WriteStatus = ParameterEditWriteStatus.Unchanged,
                    WriteMessage = null
                };
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
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
                fields[name] = field with
                {
                    PendingValue = field.LiveValue,
                    ValidationError = null,
                    WriteStatus = ParameterEditWriteStatus.Unchanged,
                    WriteMessage = null
                };
            }
        }

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public async Task<ParameterApplyReport> ApplyAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default)
    {
        return await ApplyCoreAsync(names ?? Array.Empty<string>(), null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ParameterApplyReport> ApplyAsync(ParameterWritePlan plan, IProgress<ParameterApplyProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EnsurePlanCurrent(plan);
        logger.LogInformation("Confirmed parameter write plan with {Count} entries for {VehicleId}.", plan.Entries.Count, VehicleId);
        return await ApplyCoreAsync(plan.Names ?? Array.Empty<string>(), progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ParameterApplyReport> RetryFailedAsync(ParameterApplyReport previousReport, IProgress<ParameterApplyProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previousReport);
        var retryable = previousReport.Retryable
            .Where(name => GetField(name) is { IsModified: true, IsValid: true })
            .ToArray();
        if (retryable.Length == 0)
        {
            return new ParameterApplyReport(true, [], previousReport.RebootRequired);
        }

        var retry = await ApplyCoreAsync(retryable, progress, cancellationToken).ConfigureAwait(false);
        return retry with
        {
            RebootRequired = previousReport.RebootRequired || retry.RebootRequired
        };
    }

    private async Task<ParameterApplyReport> ApplyCoreAsync(IReadOnlyList<string> names, IProgress<ParameterApplyProgress>? progress, CancellationToken cancellationToken)
    {
        Debug.Print("ApplyCoreAsync-Applying parameter edits to {0} for {1}.", names.Count, VehicleId);
        var targets = GetApplyTargets(names);
        if (cancellationToken.IsCancellationRequested)
        {
            return CancelledReport(targets);
        }

        try
        {
            await applyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CancelledReport(targets);
        }

        try
        {
            logger.LogInformation("Applying {Count} parameter edits to {VehicleId}.", targets.Count, VehicleId);
            var results = new List<ParameterWriteResult>(targets.Count);
            var rebootRequired = false;
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);

            for (var index = 0; index < targets.Count; index++)
            {
                var name = targets[index];
                ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Validating, "Validating pending value.");
                if (!TryEnsureValid(out var scopeError))
                {
                    Invalidate(scopeError);
                    AppendSkipped(targets, index, results, scopeError);
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Skipped, scopeError);
                    break;
                }

                var field = GetField(name);
                if (field is null)
                {
                    const string message = "The parameter is not loaded in this session.";
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Skipped, message));
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Skipped, message);
                    continue;
                }

                if (!field.IsModified)
                {
                    const string message = "The live value already matches the pending value.";
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Unchanged, message));
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Completed, message);
                    continue;
                }

                var validationError = Validate(field, field.PendingValue);
                if (validationError is not null)
                {
                    SetWriteState(name, ParameterEditWriteStatus.Failed, validationError, validationError);
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.ValidationFailed, validationError));
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Completed, validationError);
                    continue;
                }

                SetWriteState(name, ParameterEditWriteStatus.Applying, "Waiting for vehicle readback.", null);
                try
                {
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Writing, "Sending parameter write.");
                    var write = await WriteAndConfirmAsync(field, connectionCancellation.Token).ConfigureAwait(false);
                    if (!write.Sent)
                    {
                        const string message = "The vehicle rejected or could not send the parameter write.";
                        SetWriteState(name, ParameterEditWriteStatus.Failed, message, null);
                        results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.WriteFailed, message));
                        ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Completed, message);
                        continue;
                    }

                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Confirming, "Waiting for matching vehicle readback.");
                    if (write.Readback is null)
                    {
                        const string message = "The write was sent but matching live readback was not received before the timeout.";
                        SetWriteState(name, ParameterEditWriteStatus.Failed, message, null);
                        results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.ReadbackFailed, message));
                        ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Completed, message);
                        continue;
                    }

                    ConfirmWrite(name, write.Readback);
                    rebootRequired |= field.Metadata.RebootRequired;
                    const string confirmedMessage = "Confirmed by live vehicle readback.";
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Confirmed, confirmedMessage));
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Completed, confirmedMessage);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    const string message = "The active vehicle connection changed before the write was confirmed.";
                    Invalidate(message);
                    SetWriteState(name, ParameterEditWriteStatus.Failed, message, null);
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.ReadbackFailed, message));
                    AppendSkipped(targets, index + 1, results, message);
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Completed, message);
                    break;
                }
                catch (OperationCanceledException)
                {
                    const string message = "Parameter apply was cancelled.";
                    results.Add(new ParameterWriteResult(name, ParameterWriteOutcome.Skipped, message));
                    AppendSkipped(targets, index + 1, results, message);
                    ReportProgress(progress, index, targets.Count, name, ParameterApplyPhase.Skipped, message);
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

    private static ParameterApplyReport CancelledReport(IReadOnlyList<string> targets)
    {
        const string message = "Parameter apply was cancelled.";
        return new ParameterApplyReport(
            false,
            targets.Select(name => new ParameterWriteResult(name, ParameterWriteOutcome.Skipped, message)).ToArray(),
            false);
    }

    private void EnsurePlanCurrent(ParameterWritePlan plan)
    {
        EnsureValid();
        if (plan.Scope != Scope)
        {
            throw new InvalidOperationException("The parameter write preview targets a different vehicle or firmware.");
        }

        foreach (var entry in plan.Entries)
        {
            var field = GetField(entry.Name)
                        ?? throw new InvalidOperationException($"Parameter {entry.Name} is no longer loaded.");
            if (!Equivalent(field.LiveValue, entry.LiveValue, field.Metadata) ||
                !Equivalent(field.PendingValue, entry.PendingValue, field.Metadata) ||
                !field.IsModified ||
                Validate(field, field.PendingValue) is not null)
            {
                throw new InvalidOperationException(
                    $"The parameter write preview is stale because {entry.Name} changed after it was created.");
            }
        }
    }

    private static bool Equivalent(double left, double right, ParameterFieldMetadata? metadata)
    {
        return ParameterValueEquivalence.Default.AreEquivalent(left, right, metadata);
    }

    private static void ReportProgress(
        IProgress<ParameterApplyProgress>? progress, int zeroBasedIndex,
        int total, string name, ParameterApplyPhase phase, string message)
    {
        progress?.Report(new ParameterApplyProgress(zeroBasedIndex + 1, total, name, phase, message));
    }

    /// <inheritdoc />
    public async Task RefreshAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default)
    {
        EnsureValid();
        var targets = names is null
            ? Fields.Select(field => field.Name).ToArray()
            : names.Distinct(StringComparer.Ordinal).ToArray();
        logger.LogInformation("Refreshing {Count} edited parameters for {VehicleId}.", targets.Length, VehicleId);

        using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
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

    /// <inheritdoc/>
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
                fields[name] = fields[name] with
                {
                    WriteStatus = ParameterEditWriteStatus.Failed,
                    WriteMessage = reason
                };
            }
        }

        Changed?.Invoke();
    }

    /// <inheritdoc/>
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
        Debug.Print("LoadCoreAsync");
        var metadata = await metadataService.GetAllMetadataAsync(VehicleId, cancellationToken);
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
                    var validation = Validate(existing with
                    {
                        Metadata = projectedMetadata,
                        Type = parameter.Type
                    }, pending);
                    fields[name] = existing with
                    {
                        Type = parameter.Type,
                        LiveValue = parameter.Value,
                        PendingValue = pending,
                        Metadata = projectedMetadata,
                        ValidationError = validation,
                        WriteStatus = Equivalent(pending, parameter.Value, projectedMetadata) ? ParameterEditWriteStatus.Unchanged : ParameterEditWriteStatus.Pending,
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
        Debug.Print("exit LoadCoreAsync");

        Changed?.Invoke();
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

        void OnChanged(VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == VehicleId && args.Parameter is { } parameter &&
                parameter.Name == field.Name && Equivalent(parameter.Value, expected, field.Metadata))
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

            if (parameterRegistry.GetParameter(VehicleId, field.Name) is { } current && Equivalent(current.Value, expected, field.Metadata))
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

        Changed?.Invoke();
    }

    private void SetWriteState(string name, ParameterEditWriteStatus status, string message, string? validationError)
    {
        lock (sync)
        {
            if (!fields.TryGetValue(name, out var field))
            {
                return;
            }

            fields[name] = field with
            {
                WriteStatus = status,
                WriteMessage = message,
                ValidationError = validationError
            };
        }

        Changed?.Invoke();
    }

    private void OnParameterChanged(VehicleParameterChangedEventArgs args)
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
            var remainsModified = !Equivalent(pending, parameter.Value, field.Metadata);
            var updatedField = field with
            {
                Type = parameter.Type,
                LiveValue = parameter.Value,
                PendingValue = pending,
                ValidationError = Validate(field with
                {
                    Type = parameter.Type,
                    LiveValue = parameter.Value
                }, pending),
                WriteStatus = remainsModified
                    ? field.WriteStatus == ParameterEditWriteStatus.Applying ? ParameterEditWriteStatus.Applying : ParameterEditWriteStatus.Pending
                    : field.WriteStatus == ParameterEditWriteStatus.Applying
                        ? ParameterEditWriteStatus.Applying
                        : ParameterEditWriteStatus.Unchanged,
                WriteMessage = remainsModified ? field.WriteMessage : null
            };
            if (updatedField != field)
            {
                fields[parameter.Name] = updatedField;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
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

        if (field.Metadata.ReadOnly && !MathUtils.AreNearlyEqual(value, field.LiveValue))
        {
            return "This parameter is read-only and cannot be modified.";
        }

        if (field.Metadata.Minimum is { } minimum && value < minimum)
        {
            return $"Value must be at least {minimum}.";
        }

        if (field.Metadata.Maximum is { } maximum && value > maximum)
        {
            return $"Value must be at most {maximum}.";
        }

        if (field.Type != MavParamType.Real32 && !MathUtils.AreNearlyEqual(value, Math.Round(value)))
        {
            return "This parameter requires a whole-number value.";
        }

        var typeError = ValidateTypeRange(field.Type, value);
        if (typeError is not null)
        {
            return typeError;
        }

        if (field.Metadata.Options.Count > 0 && !field.Metadata.Options.Any(option => MathUtils.AreNearlyEqual(option.Value, value)))
        {
            return "Select one of the values advertised by the vehicle firmware metadata.";
        }

        if (field.Metadata.Bitmask.Count > 0)
        {
            if (value < 0 || !MathUtils.AreNearlyEqual(value, Math.Round(value)))
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
            if (!Equivalent(steps, Math.Round(steps), null))
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
}
