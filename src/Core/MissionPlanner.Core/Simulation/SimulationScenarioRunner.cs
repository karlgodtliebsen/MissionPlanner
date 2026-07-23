using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Simulation;

/// <summary>Provides real cancellable delays for scenario telemetry polling.</summary>
public sealed class SimulationScenarioDelay : ISimulationScenarioDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}

/// <summary>Executes the closed declarative scenario schema against one exact SITL vehicle.</summary>
public sealed class SimulationScenarioRunner : ISimulationScenarioRunner
{
    private readonly ISimulationScenarioParser parser;
    private readonly ISimulationSessionManager sessionManager;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IVehicleCommandService commandService;
    private readonly IArduPilotModeCatalog modeCatalog;
    private readonly IMissionTransferService missionTransfer;
    private readonly ISimulationControlService controlService;
    private readonly ISimulationScenarioDelay delay;
    private readonly IDateTimeProvider clock;
    private readonly SimulationScenarioOptions options;
    private readonly ILogger<SimulationScenarioRunner> logger;
    private readonly ISimulationVehicleChannelRegistry? simulationChannels;
    private readonly SemaphoreSlim runGate = new(1, 1);
    private readonly object stateLock = new();
    private SimulationScenarioRunnerSnapshot current = SimulationScenarioRunnerSnapshot.Idle;
    private bool pauseRequested;
    private TaskCompletionSource resumeSignal = CompletedSignal();

    /// <summary>Initializes the exact-target scenario runner.</summary>
    /// <param name="parser">Closed-schema parser.</param>
    /// <param name="sessionManager">Simulation session manager.</param>
    /// <param name="vehicleRegistry">Live vehicle registry.</param>
    /// <param name="commandService">Acknowledged vehicle commands.</param>
    /// <param name="modeCatalog">Firmware-specific mode catalog.</param>
    /// <param name="missionTransfer">Acknowledged mission transfer service.</param>
    /// <param name="controlService">Documented simulation controls.</param>
    /// <param name="delay">Cancellable wait provider.</param>
    /// <param name="clock">Application clock.</param>
    /// <param name="options">Execution bounds.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="simulationChannels">Optional exact routes for independently owned fleet transports.</param>
    public SimulationScenarioRunner(
        ISimulationScenarioParser parser,
        ISimulationSessionManager sessionManager,
        IVehicleRegistry vehicleRegistry,
        IVehicleCommandService commandService,
        IArduPilotModeCatalog modeCatalog,
        IMissionTransferService missionTransfer,
        ISimulationControlService controlService,
        ISimulationScenarioDelay delay,
        IDateTimeProvider clock,
        IOptions<SimulationScenarioOptions> options,
        ILogger<SimulationScenarioRunner> logger,
        ISimulationVehicleChannelRegistry? simulationChannels = null)
    {
        this.parser = parser;
        this.sessionManager = sessionManager;
        this.vehicleRegistry = vehicleRegistry;
        this.commandService = commandService;
        this.modeCatalog = modeCatalog;
        this.missionTransfer = missionTransfer;
        this.controlService = controlService;
        this.delay = delay;
        this.clock = clock;
        this.options = options.Value;
        this.logger = logger;
        this.simulationChannels = simulationChannels;
    }

    /// <inheritdoc />
    public SimulationScenarioRunnerSnapshot Current
    {
        get
        {
            lock (stateLock)
            {
                return current;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<SimulationScenarioRunnerChangedEventArgs>? Changed;

    /// <inheritdoc />
    public async Task<SimulationScenarioValidationReport> ValidateAsync(
        SimulationScenarioDocument document,
        Guid sessionId,
        VehicleId vehicleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = parser.Validate(document).ToList();
        var capabilities = new List<SimulationScenarioCapability>();
        VehicleSession? vehicle = null;
        try
        {
            vehicle = GetExactVehicle(sessionId, vehicleId);
            capabilities.Add(new SimulationScenarioCapability(
                "exact-target",
                true,
                $"Running session {sessionId:N} is connected to {vehicleId}."));
        }
        catch (Exception exception)
        {
            issues.Add(new SimulationScenarioValidationIssue(
                SimulationScenarioValidationSeverity.Error,
                "target",
                exception.Message));
            capabilities.Add(new SimulationScenarioCapability("exact-target", false, exception.Message));
        }

        if (vehicle is not null)
        {
            var family = vehicle.State.Identity.Firmware.Family;
            if (document.Steps.Any(item => item.Kind == SimulationScenarioStepKind.Takeoff))
            {
                var available = family is FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane;
                capabilities.Add(new SimulationScenarioCapability(
                    "command:takeoff",
                    available,
                    available
                        ? $"Acknowledged takeoff is supported for {family}."
                        : $"Automatic takeoff is not supported for {family}."));
            }

            if (document.Steps.Any(item => item.Kind == SimulationScenarioStepKind.Arm))
            {
                capabilities.Add(new SimulationScenarioCapability(
                    "command:arm",
                    true,
                    "Uses the existing safety-policy and ACK-tracked arm command."));
            }

            if (document.Steps.Any(item => item.Kind == SimulationScenarioStepKind.Land))
            {
                var available = modeCatalog.Find(family, VehicleMode.Land) is not null;
                capabilities.Add(new SimulationScenarioCapability(
                    "command:land",
                    available,
                    available ? $"A land mode is available for {family}." : $"No land mode is available for {family}."));
            }

            foreach (var mode in document.Steps
                         .Where(item => item.Kind == SimulationScenarioStepKind.SetMode)
                         .Select(item => item.Mode!)
                         .Where(item => !string.IsNullOrWhiteSpace(item))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var available = modeCatalog.GetModes(family)
                    .Any(item => item.Name.Equals(mode, StringComparison.OrdinalIgnoreCase));
                capabilities.Add(new SimulationScenarioCapability(
                    $"mode:{mode}",
                    available,
                    available ? $"{mode} is available for {family}." : $"{mode} is not available for {family}."));
            }
        }

        if (document.Steps.Any(item => item.Kind == SimulationScenarioStepKind.UploadMission))
        {
            capabilities.Add(new SimulationScenarioCapability(
                "mission-upload",
                true,
                "Uses the existing acknowledged MAVLink mission transfer service."));
        }

        if (document.Steps.Any(item => item.Kind == SimulationScenarioStepKind.StartMission))
        {
            capabilities.Add(new SimulationScenarioCapability(
                "mission-start",
                true,
                "Uses acknowledged MAV_CMD_MISSION_START."));
        }

        var controlKeys = document.Steps
            .Where(item => item.Kind is SimulationScenarioStepKind.InjectFault or SimulationScenarioStepKind.ClearFault)
            .Select(item => item.ControlKey!)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (vehicle is not null && controlKeys.Length > 0)
        {
            var discovered = IsPrimaryTarget(sessionId, vehicleId)
                ? await controlService.DiscoverAsync(cancellationToken).ConfigureAwait(false)
                : await controlService.DiscoverAsync(sessionId, vehicleId, cancellationToken).ConfigureAwait(false);
            foreach (var key in controlKeys)
            {
                var capability = discovered.FirstOrDefault(item => item.Descriptor.Key == key);
                var injectionSteps = document.Steps.Where(item =>
                    item.Kind == SimulationScenarioStepKind.InjectFault && item.ControlKey == key).ToArray();
                var isBoundedFault = injectionSteps.Length == 0 || capability?.Descriptor is
                    { RequiresConfirmation: true, MaximumDuration: not null };
                var durationsFit = capability?.Descriptor.MaximumDuration is not { } maximum ||
                    injectionSteps.All(item => item.DurationSeconds <= maximum.TotalSeconds);
                var available = capability?.IsAvailable == true && isBoundedFault && durationsFit;
                var reason = capability?.Reason ?? $"Unknown documented simulation control '{key}'.";
                if (capability?.IsAvailable == true && !isBoundedFault)
                {
                    reason = $"Control '{key}' is not a bounded hazardous/failure injection.";
                }
                else if (capability?.IsAvailable == true && !durationsFit)
                {
                    reason = $"Control '{key}' exceeds its {capability.Descriptor.MaximumDuration!.Value.TotalSeconds:0}-second safety bound.";
                }

                capabilities.Add(new SimulationScenarioCapability(
                    $"control:{key}",
                    available,
                    reason));
            }
        }

        return new SimulationScenarioValidationReport(issues, capabilities);
    }

    /// <inheritdoc />
    public async Task<SimulationScenarioRunReport> RunAsync(
        SimulationScenarioRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await runGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another simulation scenario is already running.");
        }

        var runId = Guid.NewGuid();
        var startedAt = clock.UtcNow;
        var reports = new List<SimulationScenarioStepReport>();
        var activeControls = new HashSet<string>(StringComparer.Ordinal);
        var airborneActionIssued = false;
        var result = SimulationScenarioRunResult.Failed;
        var summary = "Scenario validation failed.";
        SimulationScenarioValidationReport validation = new([], []);
        Publish(new SimulationScenarioRunnerSnapshot(
            SimulationScenarioRunnerState.Validating,
            runId,
            null,
            $"Validating scenario '{request.Document.Name}'."));
        logger.LogInformation(
            "Simulation scenario {ScenarioId} run {RunId} validating for session {SessionId} vehicle {VehicleId}; dry run {DryRun}.",
            request.Document.Id,
            runId,
            request.SessionId,
            request.VehicleId,
            request.DryRun);

        try
        {
            validation = await ValidateAsync(
                request.Document,
                request.SessionId,
                request.VehicleId,
                cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                summary = string.Join(" ", validation.Issues
                    .Where(item => item.Severity == SimulationScenarioValidationSeverity.Error)
                    .Select(item => item.Message)
                    .Concat(validation.Capabilities.Where(item => !item.Available).Select(item => item.Reason)));
                Publish(new SimulationScenarioRunnerSnapshot(SimulationScenarioRunnerState.Failed, runId, null, summary));
                return CreateReport(request, runId, startedAt, result, summary, validation, reports);
            }

            if (request.DryRun)
            {
                foreach (var step in request.Document.Steps)
                {
                    var now = clock.UtcNow;
                    reports.Add(new SimulationScenarioStepReport(
                        step.Id,
                        step.Name,
                        step.Kind,
                        now,
                        now,
                        SimulationScenarioStepResult.Planned,
                        DescribePlan(step),
                        CaptureTelemetry(request.VehicleId)));
                }

                result = SimulationScenarioRunResult.DryRun;
                summary = $"Dry run validated {reports.Count} steps; no vehicle-changing action was executed.";
                Publish(new SimulationScenarioRunnerSnapshot(SimulationScenarioRunnerState.Completed, runId, null, summary));
                return CreateReport(request, runId, startedAt, result, summary, validation, reports);
            }

            lock (stateLock)
            {
                pauseRequested = false;
                resumeSignal = CompletedSignal();
            }

            for (var index = 0; index < request.Document.Steps.Count; index++)
            {
                await WaitAtSafeBoundaryAsync(runId, cancellationToken).ConfigureAwait(false);
                var step = request.Document.Steps[index];
                GetExactVehicle(request.SessionId, request.VehicleId);
                if (RequiresHazardConfirmation(step.Kind) && !request.HazardousActionsConfirmed)
                {
                    var now = clock.UtcNow;
                    reports.Add(new SimulationScenarioStepReport(
                        step.Id,
                        step.Name,
                        step.Kind,
                        now,
                        now,
                        SimulationScenarioStepResult.Failed,
                        "Explicit hazardous-action confirmation was not supplied for the run.",
                        CaptureTelemetry(request.VehicleId)));
                    throw new ScenarioStepException(step, "This step requires explicit hazardous-action confirmation for the run.");
                }

                Publish(new SimulationScenarioRunnerSnapshot(
                    SimulationScenarioRunnerState.Running,
                    runId,
                    step.Id,
                    $"Running step {index + 1}/{request.Document.Steps.Count}: {step.Name}"));
                var stepStarted = clock.UtcNow;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));
                try
                {
                    var evidence = await ExecuteStepAsync(
                        request,
                        step,
                        activeControls,
                        timeout.Token).ConfigureAwait(false);
                    airborneActionIssued |= step.Kind is SimulationScenarioStepKind.Arm or SimulationScenarioStepKind.Takeoff;
                    reports.Add(new SimulationScenarioStepReport(
                        step.Id,
                        step.Name,
                        step.Kind,
                        stepStarted,
                        clock.UtcNow,
                        SimulationScenarioStepResult.Succeeded,
                        evidence,
                        CaptureTelemetry(request.VehicleId)));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var evidence = $"Step timed out after {step.TimeoutSeconds} seconds.";
                    reports.Add(new SimulationScenarioStepReport(
                        step.Id,
                        step.Name,
                        step.Kind,
                        stepStarted,
                        clock.UtcNow,
                        SimulationScenarioStepResult.Failed,
                        evidence,
                        CaptureTelemetry(request.VehicleId)));
                    throw new ScenarioStepException(step, evidence);
                }
                catch (OperationCanceledException)
                {
                    reports.Add(new SimulationScenarioStepReport(
                        step.Id,
                        step.Name,
                        step.Kind,
                        stepStarted,
                        clock.UtcNow,
                        SimulationScenarioStepResult.Canceled,
                        "Step canceled by the caller.",
                        CaptureTelemetry(request.VehicleId)));
                    throw;
                }
                catch (ScenarioStepException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    reports.Add(new SimulationScenarioStepReport(
                        step.Id,
                        step.Name,
                        step.Kind,
                        stepStarted,
                        clock.UtcNow,
                        SimulationScenarioStepResult.Failed,
                        exception.Message,
                        CaptureTelemetry(request.VehicleId)));
                    throw new ScenarioStepException(step, exception.Message, exception);
                }
            }

            result = SimulationScenarioRunResult.Succeeded;
            summary = $"All {reports.Count} scenario steps completed successfully.";
            Publish(new SimulationScenarioRunnerSnapshot(SimulationScenarioRunnerState.Completed, runId, null, summary));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = SimulationScenarioRunResult.Canceled;
            summary = "Scenario canceled; bounded control cleanup and safe landing or ground disarm were attempted.";
            Publish(new SimulationScenarioRunnerSnapshot(SimulationScenarioRunnerState.Canceled, runId, Current.StepId, summary));
        }
        catch (ScenarioStepException exception)
        {
            result = SimulationScenarioRunResult.Failed;
            summary = $"Step '{exception.Step.Name}' failed: {exception.Message}";
            Publish(new SimulationScenarioRunnerSnapshot(SimulationScenarioRunnerState.Failed, runId, exception.Step.Id, summary));
        }
        catch (Exception exception)
        {
            result = SimulationScenarioRunResult.Failed;
            summary = $"Scenario failed before a step could complete: {exception.Message}";
            Publish(new SimulationScenarioRunnerSnapshot(SimulationScenarioRunnerState.Failed, runId, Current.StepId, summary));
        }
        finally
        {
            await CleanupAsync(request, activeControls, airborneActionIssued && result != SimulationScenarioRunResult.Succeeded)
                .ConfigureAwait(false);
            lock (stateLock)
            {
                pauseRequested = false;
                resumeSignal.TrySetResult();
            }

            runGate.Release();
            logger.LogInformation(
                "Simulation scenario {ScenarioId} run {RunId} ended with {Result} after {StepCount} recorded steps.",
                request.Document.Id,
                runId,
                result,
                reports.Count);
        }

        return CreateReport(request, runId, startedAt, result, summary, validation, reports);
    }

    /// <inheritdoc />
    public bool Pause()
    {
        SimulationScenarioRunnerSnapshot snapshot;
        lock (stateLock)
        {
            if (current.State != SimulationScenarioRunnerState.Running || pauseRequested)
            {
                return false;
            }

            pauseRequested = true;
            resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            snapshot = current with
            {
                State = SimulationScenarioRunnerState.PauseRequested,
                Message = "Pause requested; the current step will finish before pausing."
            };
        }

        Publish(snapshot);
        return true;
    }

    /// <inheritdoc />
    public bool Resume()
    {
        TaskCompletionSource signal;
        lock (stateLock)
        {
            if (!pauseRequested || current.State is not (SimulationScenarioRunnerState.Paused or SimulationScenarioRunnerState.PauseRequested))
            {
                return false;
            }

            pauseRequested = false;
            signal = resumeSignal;
        }

        signal.TrySetResult();
        Publish(Current with { State = SimulationScenarioRunnerState.Running, Message = "Scenario resumed at a safe step boundary." });
        return true;
    }

    private async Task<string> ExecuteStepAsync(
        SimulationScenarioRunRequest request,
        SimulationScenarioStep step,
        ISet<string> activeControls,
        CancellationToken cancellationToken)
    {
        switch (step.Kind)
        {
            case SimulationScenarioStepKind.WaitForState:
                await WaitForAsync(
                    request,
                    state => MatchesState(state, step.State!.Value),
                    cancellationToken).ConfigureAwait(false);
                return $"Observed state {step.State}.";

            case SimulationScenarioStepKind.SetMode:
            {
                var vehicle = GetExactVehicle(request.SessionId, request.VehicleId);
                var mode = modeCatalog.GetModes(vehicle.State.Identity.Firmware.Family)
                    .First(item => item.Name.Equals(step.Mode, StringComparison.OrdinalIgnoreCase));
                return CommandEvidence(await commandService.SetModeAsync(request.VehicleId, mode, cancellationToken).ConfigureAwait(false));
            }

            case SimulationScenarioStepKind.Arm:
                return CommandEvidence(await commandService.ArmAsync(request.VehicleId, cancellationToken).ConfigureAwait(false));

            case SimulationScenarioStepKind.Takeoff:
                return CommandEvidence(await commandService.TakeoffAsync(
                    request.VehicleId,
                    ResolveNumber(step.Value!, request.Document.Variables),
                    request.HazardousActionsConfirmed,
                    cancellationToken).ConfigureAwait(false));

            case SimulationScenarioStepKind.UploadMission:
            {
                var items = step.MissionItems!.Select((item, index) => new MavLinkMissionItem(
                    checked((ushort)index),
                    item.Frame,
                    item.Command,
                    item.Current,
                    item.AutoContinue,
                    item.Param1,
                    item.Param2,
                    item.Param3,
                    item.Param4,
                    item.X,
                    item.Y,
                    item.Z,
                    MavMissionType.Mission)).ToArray();
                var upload = await missionTransfer.UploadItemsAsync(
                    request.VehicleId,
                    items,
                    MissionPlanType.FlightMission,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!upload.Success)
                {
                    throw new InvalidOperationException(upload.Error ?? $"Mission upload ACK result {upload.AckResult}.");
                }

                return $"Mission upload acknowledged for {items.Length} items (ACK {upload.AckResult ?? 0}).";
            }

            case SimulationScenarioStepKind.StartMission:
                return CommandEvidence(await commandService.ExecuteExpertAsync(
                    new ExpertVehicleCommand(request.VehicleId, MavLinkCommandIds.MissionStart, [0, 0, 0, 0, 0, 0, 0]),
                    request.HazardousActionsConfirmed,
                    cancellationToken).ConfigureAwait(false));

            case SimulationScenarioStepKind.WaitCondition:
            case SimulationScenarioStepKind.AssertTelemetry:
                await WaitForAsync(
                    request,
                    state => EvaluateCondition(state, step.Condition!, request.Document.Variables),
                    cancellationToken).ConfigureAwait(false);
                return DescribeCondition(step.Condition!, request.Document.Variables);

            case SimulationScenarioStepKind.InjectFault:
            {
                var duration = TimeSpan.FromSeconds(step.DurationSeconds!.Value);
                var value = ResolveNumber(step.Value!, request.Document.Variables);
                if (IsPrimaryTarget(request.SessionId, request.VehicleId))
                {
                    await controlService.ApplyAsync(
                        step.ControlKey!, value, duration, request.HazardousActionsConfirmed, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await controlService.ApplyAsync(
                        request.SessionId,
                        request.VehicleId,
                        step.ControlKey!,
                        value,
                        duration,
                        request.HazardousActionsConfirmed,
                        cancellationToken).ConfigureAwait(false);
                }
                activeControls.Add(step.ControlKey!);
                return $"Documented control '{step.ControlKey}' applied and confirmed for at most {duration.TotalSeconds:0} seconds.";
            }

            case SimulationScenarioStepKind.ClearFault:
                if (IsPrimaryTarget(request.SessionId, request.VehicleId))
                {
                    await controlService.ResetAsync(step.ControlKey!, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await controlService.ResetAsync(
                        request.SessionId,
                        request.VehicleId,
                        step.ControlKey!,
                        cancellationToken).ConfigureAwait(false);
                }
                activeControls.Remove(step.ControlKey!);
                return $"Documented control '{step.ControlKey}' reset and confirmed.";

            case SimulationScenarioStepKind.Land:
                return CommandEvidence(await commandService.LandAsync(request.VehicleId, cancellationToken).ConfigureAwait(false));

            default:
                throw new InvalidOperationException($"Unsupported scenario step {step.Kind}.");
        }
    }

    private async Task WaitForAsync(
        SimulationScenarioRunRequest request,
        Func<VehicleState, bool> predicate,
        CancellationToken cancellationToken)
    {
        var poll = TimeSpan.FromMilliseconds(Math.Clamp(options.PollIntervalMilliseconds, 10, 5000));
        while (true)
        {
            var state = GetExactVehicle(request.SessionId, request.VehicleId).State;
            if (predicate(state))
            {
                return;
            }

            await delay.DelayAsync(poll, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitAtSafeBoundaryAsync(Guid runId, CancellationToken cancellationToken)
    {
        Task? wait = null;
        lock (stateLock)
        {
            if (pauseRequested)
            {
                wait = resumeSignal.Task;
            }
        }

        if (wait is null)
        {
            return;
        }

        Publish(new SimulationScenarioRunnerSnapshot(
            SimulationScenarioRunnerState.Paused,
            runId,
            Current.StepId,
            "Scenario paused between steps."));
        await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CleanupAsync(
        SimulationScenarioRunRequest request,
        IEnumerable<string> activeControls,
        bool attemptLand)
    {
        foreach (var control in activeControls.ToArray())
        {
            try
            {
                GetExactVehicle(request.SessionId, request.VehicleId);
                if (IsPrimaryTarget(request.SessionId, request.VehicleId))
                {
                    await controlService.ResetAsync(control, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await controlService.ResetAsync(
                        request.SessionId,
                        request.VehicleId,
                        control,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Scenario cleanup could not reset control {ControlKey}.", control);
            }
        }

        if (!attemptLand)
        {
            return;
        }

        try
        {
            var vehicle = GetExactVehicle(request.SessionId, request.VehicleId);
            if (vehicle.State.Flight.LandedState is VehicleLandedState.InAir or VehicleLandedState.TakingOff or VehicleLandedState.Landing)
            {
                await commandService.LandAsync(request.VehicleId, CancellationToken.None).ConfigureAwait(false);
            }
            else if (vehicle.State.IsArmed && vehicle.State.Flight.LandedState == VehicleLandedState.OnGround)
            {
                await commandService.DisarmAsync(request.VehicleId, safetyConfirmed: true, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Scenario cleanup could not command a safe landing for {VehicleId}.", request.VehicleId);
        }
    }

    private SimulationScenarioRunReport CreateReport(
        SimulationScenarioRunRequest request,
        Guid runId,
        DateTimeOffset startedAt,
        SimulationScenarioRunResult result,
        string summary,
        SimulationScenarioValidationReport validation,
        IReadOnlyList<SimulationScenarioStepReport> steps) =>
        new(
            1,
            runId,
            request.Document.Id,
            request.Document.Name,
            request.SessionId,
            request.VehicleId,
            startedAt,
            clock.UtcNow,
            result,
            request.DryRun,
            summary,
            validation,
            steps,
            CaptureTelemetry(request.VehicleId));

    private VehicleSession GetExactVehicle(Guid sessionId, VehicleId vehicleId)
    {
        var snapshot = sessionManager.Current;
        var singleSessionMatches = snapshot.State == SimulationSessionState.Running &&
            snapshot.SessionId == sessionId && snapshot.VehicleId == vehicleId;
        var fleetChannelMatches = simulationChannels?.Find(vehicleId)?.SessionId == sessionId;
        if (!singleSessionMatches && !fleetChannelMatches)
        {
            throw new InvalidOperationException("The selected simulation session or VehicleId is no longer the exact running target.");
        }

        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        if (vehicle is null || vehicle.State.Connection.State != VehicleConnectionState.Online)
        {
            throw new InvalidOperationException($"Simulation vehicle {vehicleId} is not online.");
        }

        return vehicle;
    }

    private bool IsPrimaryTarget(Guid sessionId, VehicleId vehicleId)
    {
        var snapshot = sessionManager.Current;
        return snapshot.State == SimulationSessionState.Running &&
            snapshot.SessionId == sessionId && snapshot.VehicleId == vehicleId;
    }

    private SimulationTelemetrySnapshot? CaptureTelemetry(VehicleId vehicleId)
    {
        var state = vehicleRegistry.GetRequired(vehicleId)?.State;
        return state is null
            ? null
            : new SimulationTelemetrySnapshot(
                clock.UtcNow,
                state.Connection.State,
                state.Flight.Mode,
                state.Flight.IsArmed,
                state.Flight.LandedState,
                state.Position.LatitudeDegrees,
                state.Position.LongitudeDegrees,
                state.Position.AltitudeMslMeters,
                state.Position.RelativeAltitudeMeters,
                state.Motion.GroundSpeedMetersPerSecond,
                state.Power.BatteryRemainingPercent,
                state.Gps.FixType);
    }

    private static bool MatchesState(VehicleState state, SimulationVehicleStateRequirement requirement) => requirement switch
    {
        SimulationVehicleStateRequirement.Online => state.Connection.State == VehicleConnectionState.Online,
        SimulationVehicleStateRequirement.Armed => state.Flight.IsArmed,
        SimulationVehicleStateRequirement.Disarmed => !state.Flight.IsArmed,
        SimulationVehicleStateRequirement.OnGround => state.Flight.LandedState == VehicleLandedState.OnGround,
        SimulationVehicleStateRequirement.InAir => state.Flight.LandedState == VehicleLandedState.InAir,
        _ => false
    };

    private static bool EvaluateCondition(
        VehicleState state,
        SimulationTelemetryCondition condition,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables)
    {
        object? observed = condition.Metric switch
        {
            SimulationTelemetryMetric.Online => state.Connection.State == VehicleConnectionState.Online,
            SimulationTelemetryMetric.Armed => state.Flight.IsArmed,
            SimulationTelemetryMetric.Mode => state.Flight.Mode.ToString(),
            SimulationTelemetryMetric.LandedState => state.Flight.LandedState.ToString(),
            SimulationTelemetryMetric.GpsFixType => state.Gps.FixType.ToString(),
            SimulationTelemetryMetric.RelativeAltitudeMeters => state.Position.RelativeAltitudeMeters,
            SimulationTelemetryMetric.AltitudeMslMeters => state.Position.AltitudeMslMeters,
            SimulationTelemetryMetric.GroundSpeedMetersPerSecond => state.Motion.GroundSpeedMetersPerSecond,
            SimulationTelemetryMetric.BatteryRemainingPercent => state.Power.BatteryRemainingPercent is { } percent ? (double)percent : null,
            SimulationTelemetryMetric.LatitudeDegrees => state.Position.LatitudeDegrees,
            SimulationTelemetryMetric.LongitudeDegrees => state.Position.LongitudeDegrees,
            _ => null
        };
        var expected = ResolveValue(condition.Expected, variables);
        if (observed is null || expected is null)
        {
            return false;
        }

        if (observed is bool observedBoolean && expected is bool expectedBoolean)
        {
            var equal = observedBoolean == expectedBoolean;
            return condition.Operator == SimulationComparisonOperator.Equal ? equal : !equal;
        }

        if (observed is string observedText && expected is string expectedText)
        {
            var equal = observedText.Equals(expectedText, StringComparison.OrdinalIgnoreCase);
            return condition.Operator == SimulationComparisonOperator.Equal ? equal : !equal;
        }

        if (observed is double observedNumber && expected is double expectedNumber)
        {
            var tolerance = condition.Tolerance ?? 0.0001;
            return condition.Operator switch
            {
                SimulationComparisonOperator.Equal => Math.Abs(observedNumber - expectedNumber) <= tolerance,
                SimulationComparisonOperator.NotEqual => Math.Abs(observedNumber - expectedNumber) > tolerance,
                SimulationComparisonOperator.GreaterThan => observedNumber > expectedNumber,
                SimulationComparisonOperator.GreaterThanOrEqual => observedNumber >= expectedNumber,
                SimulationComparisonOperator.LessThan => observedNumber < expectedNumber,
                SimulationComparisonOperator.LessThanOrEqual => observedNumber <= expectedNumber,
                _ => false
            };
        }

        return false;
    }

    private static object? ResolveValue(
        SimulationScenarioValue value,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables)
    {
        if (!string.IsNullOrWhiteSpace(value.Variable))
        {
            value = variables[value.Variable];
        }

        return value.Kind switch
        {
            SimulationScenarioValueKind.Boolean => value.BooleanValue,
            SimulationScenarioValueKind.Number => value.NumberValue,
            SimulationScenarioValueKind.Text => value.TextValue,
            _ => null
        };
    }

    private static double ResolveNumber(
        SimulationScenarioValue value,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables) =>
        ResolveValue(value, variables) is double number
            ? number
            : throw new InvalidOperationException("The scenario value did not resolve to a number.");

    private static string CommandEvidence(VehicleCommandResponse response)
    {
        if (response.Result != VehicleCommandResult.Accepted)
        {
            throw new InvalidOperationException(response.Message ?? $"Vehicle command result was {response.Result}.");
        }

        return response.Message ?? $"Vehicle command acknowledged as {response.Result}.";
    }

    private static bool RequiresHazardConfirmation(SimulationScenarioStepKind kind) => kind is
        SimulationScenarioStepKind.Arm or
        SimulationScenarioStepKind.Takeoff or
        SimulationScenarioStepKind.StartMission or
        SimulationScenarioStepKind.InjectFault;

    private static string DescribePlan(SimulationScenarioStep step) =>
        $"Validated {step.Kind} with explicit {step.TimeoutSeconds}-second timeout; no action executed.";

    private static string DescribeCondition(
        SimulationTelemetryCondition condition,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables) =>
        $"Observed {condition.Metric} {condition.Operator} {Convert.ToString(ResolveValue(condition.Expected, variables), CultureInfo.InvariantCulture)}.";

    private void Publish(SimulationScenarioRunnerSnapshot snapshot)
    {
        lock (stateLock)
        {
            current = snapshot;
        }

        Changed?.Invoke(this, new SimulationScenarioRunnerChangedEventArgs(snapshot));
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        result.TrySetResult();
        return result;
    }

    private sealed class ScenarioStepException : Exception
    {
        public ScenarioStepException(SimulationScenarioStep step, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Step = step;
        }

        public SimulationScenarioStep Step { get; }
    }
}

/// <summary>Exports complete scenario evidence as versioned JSON or readable text.</summary>
public sealed class SimulationScenarioReportExporter : ISimulationScenarioReportExporter
{
    private static readonly JsonSerializerOptions jsonOptions = CreateOptions();

    /// <inheritdoc />
    public string ToJson(SimulationScenarioRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, jsonOptions);
    }

    /// <inheritdoc />
    public string ToText(SimulationScenarioRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.AppendLine($"Simulation scenario: {report.ScenarioName}");
        builder.AppendLine($"Result: {report.Result} — {report.Summary}");
        builder.AppendLine($"Run: {report.RunId:N}");
        builder.AppendLine($"Target: session {report.SessionId:N}, vehicle {report.VehicleId}");
        builder.AppendLine($"Started: {report.StartedAt:O}");
        builder.AppendLine($"Ended: {report.EndedAt:O}");
        builder.AppendLine("Steps:");
        foreach (var step in report.Steps)
        {
            builder.AppendLine($"- [{step.Result}] {step.StepId} {step.Name}: {step.Evidence}");
        }

        if (report.Validation.Capabilities.Count > 0)
        {
            builder.AppendLine("Capabilities:");
            foreach (var capability in report.Validation.Capabilities)
            {
                builder.AppendLine($"- [{(capability.Available ? "available" : "unavailable")}] {capability.Name}: {capability.Reason}");
            }
        }

        return builder.ToString();
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        result.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return result;
    }
}
