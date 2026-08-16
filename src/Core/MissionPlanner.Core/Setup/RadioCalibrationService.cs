using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Firmware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
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

    private readonly Lock sync = new();
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
    private bool workflowActive;
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
            SeedCaptures(state, parameterRegistry.GetAllParameters(vehicleId));
            workflowActive = true;
        }

        StartObservingVehicle();
        Transition(new RadioCalibrationSnapshot(
            vehicleId,
            RadioCalibrationState.Capturing,
            SnapshotCaptures(),
            "Move every stick and control through its full travel, then finish endpoint capture. No parameters are written at that point.",
            []));
        logger.LogInformation("Started radio calibration capture for {VehicleId}.", vehicleId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RadioCalibrationSnapshot> FinishCaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        RadioCalibrationSnapshot next;
        lock (sync)
        {
            if (Current.State != RadioCalibrationState.Capturing || Current.VehicleId is not { } target)
            {
                throw new InvalidOperationException("Start radio calibration capture before finishing endpoint discovery.");
            }

            var parameters = parameterRegistry.GetAllParameters(target);
            ApplyChannelSemantics(parameters, activeVehicle.State);
            var snapshot = SnapshotCaptures();
            var issues = ValidateCaptures(snapshot, ResolveFunctions(parameters));
            if (issues.Any(issue => issue.Severity == RadioIssueSeverity.Hazard))
            {
                next = Current with
                {
                    Captures = snapshot,
                    Instruction = "Endpoint capture is incomplete. Move every listed control through its full travel, then try again.",
                    Issues = issues,
                    FailureReason = "One or more channels failed endpoint validation."
                };
            }
            else
            {
                next = new RadioCalibrationSnapshot(
                    target,
                    RadioCalibrationState.Review,
                    snapshot,
                    ReviewInstruction(snapshot),
                    issues);
            }
        }

        Transition(next);
        return Task.FromResult(next);
    }

    /// <inheritdoc />
    public async Task<RadioWriteResult> CompleteAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        VehicleId vehicleId;
        IReadOnlyList<RadioChannelCapture> snapshot;
        VehicleState state;
        lock (sync)
        {
            if (Current.State != RadioCalibrationState.Review || Current.VehicleId is not { } target)
            {
                throw new InvalidOperationException("Finish endpoint capture and review the fresh trim positions before writing.");
            }

            if (!activeVehicle.IsOnline || activeVehicle.VehicleId != target || activeVehicle.State is not { } currentState)
            {
                return Fail(target, SnapshotCaptures(), Current.Issues, "The target vehicle is no longer connected.");
            }

            if (currentState.IsArmed)
            {
                return Fail(target, SnapshotCaptures(), Current.Issues, "Disarm the vehicle before writing radio calibration.");
            }

            if (currentState.Radio.IsStale(clock.UtcNow, staleWindow))
            {
                return Fail(target, SnapshotCaptures(), Current.Issues, "Fresh RC input is required before writing calibration.");
            }

            vehicleId = target;
            state = currentState;
            SampleReviewTrims(state);
            snapshot = SnapshotCaptures();
        }

        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var functions = ResolveFunctions(parameters);
        var issues = ValidateTrimCandidates(snapshot, functions);
        if (issues.Any(issue => issue.Severity == RadioIssueSeverity.Hazard))
        {
            Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Review, snapshot,
                ReviewInstruction(snapshot), issues,
                "One or more fresh trim candidates failed validation."));
            return new RadioWriteResult(false, "Trim candidates failed validation and no parameters were written.");
        }

        Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Writing, snapshot,
            "Writing and confirming radio endpoints and trims…", issues));
        try
        {
            var writes = BuildWritePlan(snapshot);
            foreach (var write in writes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureVehicleSafeForWrite(vehicleId);
                if (!await WriteAndConfirmAsync(vehicleId, write.Name, write.Value, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(vehicleId, snapshot, issues, $"Readback did not confirm {write.Name}={write.Value}.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            workflowActive = false;
            StopObservingVehicle();
            ReleaseLease();
            Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Cancelled, snapshot,
                "Calibration was cancelled during the write. Refresh values before flying.", issues));
            return new RadioWriteResult(false, "Calibration write was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Radio calibration write failed for {VehicleId}.", vehicleId);
            return Fail(vehicleId, snapshot, issues, exception.Message);
        }

        ReleaseLease();
        workflowActive = false;
        StopObservingVehicle();
        Transition(new RadioCalibrationSnapshot(vehicleId, RadioCalibrationState.Success, snapshot,
            "Radio endpoints and trims written and confirmed by readback.", issues));
        logger.LogInformation("Radio calibration confirmed for {VehicleId}.", vehicleId);
        return new RadioWriteResult(true, "Endpoints written and confirmed by readback.");
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (Current.State is not (RadioCalibrationState.Capturing or RadioCalibrationState.Review))
        {
            return Task.CompletedTask;
        }

        lock (sync)
        {
            workflowActive = false;
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
        if (Current.State is RadioCalibrationState.Capturing or RadioCalibrationState.Review or RadioCalibrationState.Writing)
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
        if (!workflowActive || Current.VehicleId is not { } vehicleId)
        {
            return;
        }

        if (!args.Current.IsOnline || args.Current.VehicleId != vehicleId)
        {
            lock (sync)
            {
                workflowActive = false;
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
            if (!workflowActive || Current.VehicleId != evt.VehicleId)
            {
                return Task.CompletedTask;
            }

            if (Current.State == RadioCalibrationState.Capturing)
            {
                UpdateCaptures(evt.VehicleState);
            }
            else if (Current.State == RadioCalibrationState.Review)
            {
                UpdateReviewValues(evt.VehicleState);
            }
            else
            {
                return Task.CompletedTask;
            }
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

    private void SeedCaptures(VehicleState state, IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        var raw = state.Radio.ChannelsRaw;
        for (var index = 0; index < raw.Count; index++)
        {
            var pwm = raw[index];
            if (pwm != 0)
            {
                var number = index + 1;
                captures[number] = new RadioChannelCapture(number, pwm, pwm, pwm);
            }
        }

        ApplyChannelSemantics(parameters, state);
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
                captures[number] = existing with { Minimum = Math.Min(existing.Minimum, pwm), Maximum = Math.Max(existing.Maximum, pwm), Current = pwm };
            }
            else
            {
                captures[number] = new RadioChannelCapture(number, pwm, pwm, pwm);
            }
        }
    }

    private void UpdateReviewValues(VehicleState state)
    {
        var raw = state.Radio.ChannelsRaw;
        foreach (var number in captures.Keys.ToArray())
        {
            if (number <= raw.Count && raw[number - 1] != 0)
            {
                var pwm = raw[number - 1];
                captures[number] = captures[number] with { Current = pwm, CandidateTrim = pwm };
            }
        }
    }

    private void SampleReviewTrims(VehicleState state)
    {
        UpdateReviewValues(state);
    }

    private void ApplyChannelSemantics(IReadOnlyDictionary<string, VehicleParameter> parameters, VehicleState? state)
    {
        var functions = ResolveFunctions(parameters);
        var reversibleThrottle = state?.Identity.Firmware.Family is FirmwareFamily.ArduPlane or FirmwareFamily.Rover &&
                                 ReadInt(parameters, "THR_MIN", 0) < 0;
        foreach (var number in captures.Keys.ToArray())
        {
            var function = functions.GetValueOrDefault(number);
            var policy = function switch
            {
                "Roll" or "Pitch" or "Yaw" => RadioTrimPolicy.Centered,
                "Throttle" when reversibleThrottle => RadioTrimPolicy.Centered,
                "Throttle" => RadioTrimPolicy.Low,
                _ => RadioTrimPolicy.Current
            };
            captures[number] = captures[number] with
            {
                FunctionName = function,
                TrimPolicy = policy,
                CandidateTrim = null,
                Issues = []
            };
        }
    }

    private static string ReviewInstruction(IReadOnlyList<RadioChannelCapture> snapshot)
    {
        var throttleInstruction = snapshot.Any(capture => capture.FunctionName == "Throttle" && capture.TrimPolicy == RadioTrimPolicy.Centered)
            ? "Center the reversible throttle at neutral."
            : "Place conventional throttle fully low.";
        return $"Endpoint capture is complete. Center Roll, Pitch, and Yaw with transmitter trims neutral. {throttleInstruction} Review the live trim candidates, then confirm and write.";
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
        workflowActive = false;
        StopObservingVehicle();
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

    private static IReadOnlyList<RadioValidationIssue> ValidateTrimCandidates(
        IReadOnlyList<RadioChannelCapture> captures,
        IReadOnlyDictionary<int, string> functions)
    {
        var issues = new List<RadioValidationIssue>();
        foreach (var capture in captures.Where(item => item.Range >= MinimumTravel))
        {
            if (capture.CandidateTrim is not { } trim || trim < capture.Minimum || trim > capture.Maximum)
            {
                issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                    $"Channel {capture.Number}{Label(functions.GetValueOrDefault(capture.Number))} trim is outside its captured range."));
                continue;
            }

            if (capture.TrimPolicy == RadioTrimPolicy.Centered)
            {
                var minimumDistance = Math.Max(50, capture.Range / 10);
                if (trim - capture.Minimum < minimumDistance || capture.Maximum - trim < minimumDistance)
                {
                    issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                        $"Channel {capture.Number}{Label(functions.GetValueOrDefault(capture.Number))} is too close to an endpoint to use as a centered trim."));
                }
            }
            else if (capture.TrimPolicy == RadioTrimPolicy.Low && trim > capture.Minimum + capture.Range / 4)
            {
                issues.Add(new RadioValidationIssue(RadioIssueSeverity.Hazard,
                    $"Channel {capture.Number} (Throttle) is not at the low end of its captured travel."));
            }
        }

        return issues;
    }

    private static IReadOnlyList<RadioParameterWrite> BuildWritePlan(IReadOnlyList<RadioChannelCapture> captures)
    {
        var writes = new List<RadioParameterWrite>();
        foreach (var capture in captures.Where(item => item.Range >= MinimumTravel))
        {
            if (capture.CandidateTrim is not { } trim)
            {
                throw new InvalidOperationException($"Channel {capture.Number} has no Review-stage trim candidate.");
            }

            writes.Add(new RadioParameterWrite($"RC{capture.Number}_MIN", capture.Minimum));
            writes.Add(new RadioParameterWrite($"RC{capture.Number}_MAX", capture.Maximum));
            writes.Add(new RadioParameterWrite($"RC{capture.Number}_TRIM", trim));
        }

        return writes;
    }

    private void EnsureVehicleSafeForWrite(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
        {
            throw new InvalidOperationException("The vehicle disconnected during radio calibration write.");
        }

        if (activeVehicle.State?.IsArmed != false)
        {
            throw new InvalidOperationException("The vehicle armed during radio calibration write.");
        }
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

        var duplicates = ResolvePilotAssignments(parameters)
            .GroupBy(assignment => assignment.Channel)
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
        foreach (var assignment in ResolvePilotAssignments(parameters))
        {
            functions[assignment.Channel] = functions.TryGetValue(assignment.Channel, out var existing)
                ? $"{existing}/{assignment.Function}"
                : assignment.Function;
        }

        return functions;
    }

    private static IReadOnlyList<PilotAssignment> ResolvePilotAssignments(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return pilotFunctions
            .Select(item => new PilotAssignment(item.Parameter, ReadInt(parameters, item.Parameter, item.Default), item.Function))
            .ToArray();
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

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

    private static string Label(string? function)
    {
        return function is null ? string.Empty : $" ({function})";
    }

    private static int ReadInt(IReadOnlyDictionary<string, VehicleParameter> parameters, string name, int fallback)
    {
        return parameters.TryGetValue(name, out var parameter) ? (int)Math.Round(parameter.Value) : fallback;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, VehicleParameter> parameters, string name)
    {
        return parameters.TryGetValue(name, out var parameter) && parameter.Value != 0;
    }

    private sealed record PilotAssignment(string Parameter, int Channel, string Function);

    private sealed record RadioParameterWrite(string Name, int Value);
}
