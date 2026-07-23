using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Setup;

/// <summary>Projects live RC channels and runs guarded endpoint calibration from live telemetry extremes.</summary>
public sealed class RadioCalibrationService : IRadioCalibrationService
{
    private static readonly TimeSpan staleWindow = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private const int MinimumPlausiblePwm = 800;
    private const int MaximumPlausiblePwm = 2200;
    private const int MinimumTravel = 200;
    private static readonly (string Parameter, int Default, string Function)[] pilotFunctions =
    [
        ("RCMAP_ROLL", 1, "Roll"),
        ("RCMAP_PITCH", 2, "Pitch"),
        ("RCMAP_THROTTLE", 3, "Throttle"),
        ("RCMAP_YAW", 4, "Yaw")
    ];
    private readonly object sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterService parameterService;
    private readonly IVehicleOperationGate operationGate;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<RadioCalibrationService> logger;
    private readonly Dictionary<int, RadioChannelCapture> captures = [];
    private IDisposable? operationLease;
    private IDisposable? stateSubscription;
    private bool capturing;
    private bool disposed;

    /// <summary>Initializes the radio calibration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="operationGate">The shared vehicle operation gate.</param>
    /// <param name="domainEventHub">The domain event hub used for live vehicle state.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public RadioCalibrationService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterService parameterService,
        IVehicleOperationGate operationGate,
        IDomainEventHub domainEventHub,
        IDateTimeProvider clock,
        ILogger<RadioCalibrationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.parameterService = parameterService;
        this.operationGate = operationGate;
        this.domainEventHub = domainEventHub;
        this.clock = clock;
        this.logger = logger;
    }

    /// <inheritdoc />
    public RadioCalibrationSnapshot Current { get; private set; } = RadioCalibrationSnapshot.Initial;

    /// <inheritdoc />
    public event EventHandler<RadioCalibrationStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public RadioChannelsView GetLiveChannels(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            return RadioChannelsView.Empty(vehicleId);
        }

        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var functions = ResolveFunctions(parameters);
        var channels = new List<RadioChannelInfo>();
        var raw = state.Radio.ChannelsRaw;
        for (var index = 0; index < raw.Count; index++)
        {
            var number = index + 1;
            var pwm = raw[index];
            if (pwm == 0)
            {
                continue;
            }

            var minimum = ReadInt(parameters, $"RC{number}_MIN", 1000);
            var maximum = ReadInt(parameters, $"RC{number}_MAX", 2000);
            var trim = ReadInt(parameters, $"RC{number}_TRIM", 1500);
            var reversed = ReadBool(parameters, $"RC{number}_REVERSED");
            channels.Add(new RadioChannelInfo(
                number, pwm, Normalize(pwm, minimum, maximum, trim, reversed),
                minimum, maximum, trim, reversed, functions.GetValueOrDefault(number)));
        }

        var stale = state.Radio.IsStale(clock.UtcNow, staleWindow);
        return new RadioChannelsView(vehicleId, channels, stale, DetectStaticIssues(parameters, functions));
    }

    /// <inheritdoc />
    public Task StartAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var state = activeVehicle.State;
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || state is null)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        if (state.IsArmed)
        {
            throw new InvalidOperationException("Disarm the vehicle before radio calibration.");
        }

        lock (sync)
        {
            if (Current.State == RadioCalibrationState.Capturing)
            {
                throw new InvalidOperationException("Radio calibration capture is already active.");
            }

            if (!operationGate.TryAcquire(vehicleId, "radio calibration", out operationLease))
            {
                throw new InvalidOperationException($"Cannot start calibration while {operationGate.GetCurrentOperation(vehicleId)} is active.");
            }

            captures.Clear();
            SeedCaptures(state);
            capturing = true;
        }

        StartObservingVehicle();
        Transition(new RadioCalibrationSnapshot(
            vehicleId,
            RadioCalibrationState.Capturing,
            SnapshotCaptures(),
            "Move every stick and switch to its full travel, then select Finish to review and write endpoints.",
            []));
        logger.LogInformation("Started radio calibration capture for {VehicleId}.", vehicleId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<RadioWriteResult> CompleteAsync(CancellationToken cancellationToken = default)
    {
        VehicleId vehicleId;
        IReadOnlyList<RadioChannelCapture> snapshot;
        lock (sync)
        {
            if (Current.State != RadioCalibrationState.Capturing || Current.VehicleId is not { } target)
            {
                throw new InvalidOperationException("Start radio calibration capture before finishing.");
            }

            vehicleId = target;
            snapshot = SnapshotCaptures();
            capturing = false;
        }

        StopObservingVehicle();
        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var functions = ResolveFunctions(parameters);
        var issues = ValidateCaptures(snapshot, functions);
        if (issues.Any(issue => issue.Severity == RadioIssueSeverity.Hazard))
        {
            ReleaseLease();
            Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Failed, snapshot,
                "Calibration values were rejected. Resolve the listed issues and recalibrate.", issues,
                "One or more channels failed endpoint validation."));
            return new RadioWriteResult(false, "Calibration values failed validation and were not written.");
        }

        Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Writing, snapshot,
            "Writing and confirming endpoints…", issues));
        var throttle = functions.FirstOrDefault(pair => pair.Value == "Throttle").Key;
        try
        {
            foreach (var capture in snapshot.Where(item => item.Range >= MinimumTravel))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await WriteAndConfirmAsync(vehicleId, $"RC{capture.Number}_MIN", capture.Minimum, cancellationToken).ConfigureAwait(false) ||
                    !await WriteAndConfirmAsync(vehicleId, $"RC{capture.Number}_MAX", capture.Maximum, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(vehicleId, snapshot, issues, $"Readback did not confirm endpoints for channel {capture.Number}.");
                }

                // Throttle trim is left untouched; sticks trim to their captured centre position.
                if (capture.Number != throttle && !await WriteAndConfirmAsync(vehicleId, $"RC{capture.Number}_TRIM", capture.Current, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(vehicleId, snapshot, issues, $"Readback did not confirm trim for channel {capture.Number}.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            ReleaseLease();
            Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Cancelled, snapshot,
                "Calibration was cancelled during the write. Refresh values before flying.", issues));
            return new RadioWriteResult(false, "Calibration write was cancelled.");
        }

        ReleaseLease();
        Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Success, snapshot,
            "Endpoints written and confirmed by readback.", issues));
        logger.LogInformation("Radio calibration confirmed for {VehicleId}.", vehicleId);
        return new RadioWriteResult(true, "Endpoints written and confirmed by readback.");
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (Current.State != RadioCalibrationState.Capturing)
        {
            return Task.CompletedTask;
        }

        lock (sync)
        {
            capturing = false;
        }

        StopObservingVehicle();
        ReleaseLease();
        Transition(new RadioCalibrationSnapshot(Current.VehicleId, RadioCalibrationState.Cancelled, SnapshotCaptures(),
            "Calibration cancelled. No endpoints were changed.", []));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Reset()
    {
        if (Current.State == RadioCalibrationState.Capturing)
        {
            throw new InvalidOperationException("Cancel the active radio calibration before resetting it.");
        }

        lock (sync)
        {
            captures.Clear();
        }

        Transition(RadioCalibrationSnapshot.Initial);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        StopObservingVehicle();
        ReleaseLease();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (!capturing || Current.VehicleId is not { } vehicleId)
        {
            return;
        }

        if (!args.Current.IsOnline || args.Current.VehicleId != vehicleId)
        {
            lock (sync)
            {
                capturing = false;
            }

            StopObservingVehicle();
            ReleaseLease();
            Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Disconnected, SnapshotCaptures(),
                "Vehicle disconnected during calibration. Reconnect and restart the workflow.", [],
                "Vehicle disconnected during radio calibration."));
            return;
        }

    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        RadioCalibrationSnapshot next;
        lock (sync)
        {
            if (!capturing || Current.VehicleId != evt.VehicleId)
            {
                return Task.CompletedTask;
            }

            UpdateCaptures(evt.VehicleState);
            next = Current with { Captures = SnapshotCaptures() };
        }

        Transition(next);
        return Task.CompletedTask;
    }

    private void StartObservingVehicle()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        stateSubscription?.Dispose();
        stateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
    }

    private void StopObservingVehicle()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        stateSubscription?.Dispose();
        stateSubscription = null;
    }

    private void SeedCaptures(VehicleState state)
    {
        var raw = state.Radio.ChannelsRaw;
        for (var index = 0; index < raw.Count; index++)
        {
            var pwm = raw[index];
            if (pwm != 0)
            {
                captures[index + 1] = new RadioChannelCapture(index + 1, pwm, pwm, pwm);
            }
        }
    }

    private void UpdateCaptures(VehicleState state)
    {
        var raw = state.Radio.ChannelsRaw;
        for (var index = 0; index < raw.Count; index++)
        {
            var pwm = raw[index];
            if (pwm == 0)
            {
                continue;
            }

            var number = index + 1;
            if (captures.TryGetValue(number, out var existing))
            {
                captures[number] = existing with
                {
                    Minimum = Math.Min(existing.Minimum, pwm),
                    Maximum = Math.Max(existing.Maximum, pwm),
                    Current = pwm
                };
            }
            else
            {
                captures[number] = new RadioChannelCapture(number, pwm, pwm, pwm);
            }
        }
    }

    private IReadOnlyList<RadioChannelCapture> SnapshotCaptures()
    {
        lock (sync)
        {
            return captures.Values.OrderBy(capture => capture.Number).ToArray();
        }
    }

    private RadioWriteResult Fail(VehicleId vehicleId, IReadOnlyList<RadioChannelCapture> snapshot, IReadOnlyList<RadioValidationIssue> issues, string reason)
    {
        ReleaseLease();
        Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Failed, snapshot,
            reason, issues, reason));
        return new RadioWriteResult(false, reason);
    }

    private static IReadOnlyList<RadioValidationIssue> ValidateCaptures(IReadOnlyList<RadioChannelCapture> captures, IReadOnlyDictionary<int, string> functions)
    {
        var issues = new List<RadioValidationIssue>();
        var primary = functions.Keys.ToHashSet();
        foreach (var capture in captures)
        {
            var function = functions.GetValueOrDefault(capture.Number);
            if (capture.Minimum >= capture.Maximum || capture.Minimum < MinimumPlausiblePwm || capture.Maximum > MaximumPlausiblePwm)
            {
                issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                    $"Channel {capture.Number}{Label(function)} produced an invalid range ({capture.Minimum}-{capture.Maximum} us)."));
            }
            else if (primary.Contains(capture.Number) && capture.Range < MinimumTravel)
            {
                issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                    $"Channel {capture.Number}{Label(function)} moved only {capture.Range} us. Move it fully and recalibrate."));
            }
        }

        foreach (var number in primary.Where(number => captures.All(capture => capture.Number != number)))
        {
            issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                $"No RC data was captured for channel {number} ({functions[number]}). Check the transmitter and receiver."));
        }

        return issues;
    }

    private IReadOnlyList<RadioValidationIssue> DetectStaticIssues(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<int, string> functions)
    {
        var issues = new List<RadioValidationIssue>();
        var throttle = functions.FirstOrDefault(pair => pair.Value == "Throttle").Key;
        if (throttle > 0 && ReadBool(parameters, $"RC{throttle}_REVERSED"))
        {
            issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                $"The throttle channel ({throttle}) is reversed. Confirm this is intended before flight."));
        }

        var duplicates = functions
            .GroupBy(pair => pair.Key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var channel in duplicates)
        {
            issues.Add(new RadioValidationIssue(RadioIssueSeverity.Warning,
                $"Multiple pilot functions are mapped to channel {channel}. Review the RCMAP assignments."));
        }

        return issues;
    }

    private static IReadOnlyDictionary<int, string> ResolveFunctions(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        var functions = new Dictionary<int, string>();
        foreach (var (parameter, defaultChannel, function) in pilotFunctions)
        {
            var channel = ReadInt(parameters, parameter, defaultChannel);
            functions[channel] = functions.TryGetValue(channel, out var existing) ? $"{existing}/{function}" : function;
        }

        return functions;
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, string name, int value, CancellationToken cancellationToken)
    {
        var type = parameterRegistry.GetParameter(vehicleId, name)?.Type ?? MavParamType.Int16;
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter && parameter.Name == name &&
                Math.Abs(parameter.Value - value) <= 0.5f)
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, name, value, type, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, name) is { } current && Math.Abs(current.Value - value) <= 0.5f)
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
            await readback.Task.WaitAsync(readbackTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            parameterRegistry.Changed -= OnChanged;
        }
    }

    private void ReleaseLease()
    {
        operationLease?.Dispose();
        operationLease = null;
    }

    private void Transition(RadioCalibrationSnapshot snapshot)
    {
        Current = snapshot;
        StateChanged?.Invoke(this, new RadioCalibrationStateChangedEventArgs(snapshot));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static double Normalize(int pwm, int minimum, int maximum, int trim, bool reversed)
    {
        double value;
        if (pwm >= trim)
        {
            value = maximum > trim ? (double)(pwm - trim) / (maximum - trim) : 0;
        }
        else
        {
            value = trim > minimum ? -(double)(trim - pwm) / (trim - minimum) : 0;
        }

        value = Math.Clamp(value, -1, 1);
        return reversed ? -value : value;
    }

    private static string Label(string? function) => function is null ? string.Empty : $" ({function})";

    private static int ReadInt(IReadOnlyDictionary<string, VehicleParameter> parameters, string name, int fallback) =>
        parameters.TryGetValue(name, out var parameter) ? (int)Math.Round(parameter.Value) : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, VehicleParameter> parameters, string name) =>
        parameters.TryGetValue(name, out var parameter) && parameter.Value != 0;
}
