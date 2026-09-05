using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Adjustments;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Presents safety-aware, acknowledged actions for the active ArduPilot vehicle.
/// </summary>
public partial class ActionsTabViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleCommandService commandService;
    private readonly IVehicleCommandPolicy commandPolicy;
    private readonly IArduPilotModeCatalog modeCatalog;
    private readonly IUserConfirmationService confirmationService;
    private readonly IUserNotificationService notificationService;
    private readonly IDomainEventHub domainEventHub;
    private readonly AsyncOperationRunner operationRunner;
    private readonly IReplaySessionManager? replaySessionManager;
    private readonly ILocalAltitudeReferenceService altitudeReferenceService;
    private readonly IMissionInterventionService missionInterventionService;
    private readonly IOnboardMissionSnapshotStore missionSnapshots;
    private readonly IVehicleAdjustmentService adjustmentService;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private IDisposable? stateSubscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionsTabViewModel"/> class.
    /// </summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="commandService">The acknowledged vehicle command service.</param>
    /// <param name="commandPolicy">The vehicle safety policy.</param>
    /// <param name="modeCatalog">The firmware-specific mode catalog.</param>
    /// <param name="confirmationService">The hazardous-action confirmation service.</param>
    /// <param name="notificationService">The separate application-notification stream.</param>
    /// <param name="domainEventHub">The domain event hub used for active-vehicle state updates.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="altitudeReferenceService"></param>
    /// <param name="missionInterventionService"></param>
    /// <param name="missionSnapshots"></param>
    /// <param name="adjustmentService"></param>
    /// <param name="parameterRegistry"></param>
    /// <param name="replaySessionManager">Optional application-wide replay safety state.</param>
    public ActionsTabViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleCommandService commandService,
        IVehicleCommandPolicy commandPolicy,
        IArduPilotModeCatalog modeCatalog,
        IUserConfirmationService confirmationService,
        IUserNotificationService notificationService, IDomainEventHub domainEventHub,
        ILogger<ActionsTabViewModel> logger,
        ILocalAltitudeReferenceService altitudeReferenceService,
        IMissionInterventionService missionInterventionService,
        IOnboardMissionSnapshotStore missionSnapshots,
        IVehicleAdjustmentService adjustmentService,
        IVehicleParameterRegistry parameterRegistry,
        IReplaySessionManager? replaySessionManager = null) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.commandService = commandService;
        this.commandPolicy = commandPolicy;
        this.modeCatalog = modeCatalog;
        this.confirmationService = confirmationService;
        this.notificationService = notificationService;
        this.domainEventHub = domainEventHub;
        this.altitudeReferenceService = altitudeReferenceService;
        this.missionInterventionService = missionInterventionService;
        this.missionSnapshots = missionSnapshots;
        this.adjustmentService = adjustmentService;
        this.parameterRegistry = parameterRegistry;
        this.replaySessionManager = replaySessionManager;
        operationRunner = new AsyncOperationRunner(activeVehicle);
    }

    /// <summary>Gets the modes appropriate to the connected firmware family.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<VehicleModeOption> Modes { get; private set; } = [];

    /// <summary>Gets or sets the selected family-specific flight mode.</summary>
    [ObservableProperty]
    public partial VehicleModeOption? SelectedMode
    {
        get; set;
    }

    /// <summary>Gets or sets the requested takeoff altitude in metres.</summary>
    [ObservableProperty]
    public partial double TakeoffAltitudeMeters { get; set; } = 10;

    /// <summary>Gets or sets the expert MAV_CMD identifier text.</summary>
    [ObservableProperty]
    public partial string ExpertCommandId { get; set; } = string.Empty;

    /// <summary>Gets or sets seven invariant-culture expert COMMAND_LONG parameters.</summary>
    [ObservableProperty]
    public partial string ExpertParameters { get; set; } = "0 0 0 0 0 0 0";

    /// <summary>Gets or sets whether the advanced expert command section is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpertSectionVisible
    {
        get; set;
    }

    /// <summary>Gets the command currently awaiting acknowledgement or telemetry confirmation.</summary>
    [ObservableProperty]
    public partial string? PendingCommand
    {
        get; private set;
    }

    /// <summary>Gets the latest acknowledgement description.</summary>
    [ObservableProperty]
    public partial string AckResult { get; private set; } = "No command sent";

    /// <summary>Gets the latest state actually observed in vehicle telemetry.</summary>
    [ObservableProperty]
    public partial string ObservedState { get; private set; } = "No active vehicle";

    /// <summary>Gets the current asynchronous command presentation state.</summary>
    [ObservableProperty]
    public partial AsyncOperationState OperationState { get; private set; } = AsyncOperationState.Idle;

    /// <summary>Gets whether arm is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanArm
    {
        get; private set;
    }

    /// <summary>Gets whether disarm is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanDisarm
    {
        get; private set;
    }

    /// <summary>Gets whether takeoff is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanTakeoff
    {
        get; private set;
    }

    /// <summary>Gets whether landing is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanLand
    {
        get; private set;
    }

    /// <summary>Gets whether holding position is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanHoldPosition
    {
        get; private set;
    }

    /// <summary>Gets whether returning to launch is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanReturnToLaunch
    {
        get; private set;
    }

    /// <summary>Gets whether reboot is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanReboot
    {
        get; private set;
    }

    /// <summary>Gets whether setting home is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanSetHome
    {
        get; private set;
    }

    /// <summary>Gets whether the local relative-altitude reference can be toggled.</summary>
    [ObservableProperty]
    public partial bool CanToggleAltitudeZero
    {
        get; private set;
    }

    /// <summary>Gets the local display operation label.</summary>
    [ObservableProperty]
    public partial string AltitudeZeroActionText { get; private set; } = "Zero Altitude";

    [ObservableProperty]
    public partial double SelectedMissionSequence
    {
        get; set;
    }

    [ObservableProperty]
    public partial string CurrentMissionSequenceText { get; private set; } = "Current sequence: unknown";

    [ObservableProperty]
    public partial bool CanSetCurrentMissionItem
    {
        get; private set;
    }

    [ObservableProperty]
    public partial bool CanRestartMission
    {
        get; private set;
    }

    [ObservableProperty]
    public partial bool CanResumeMission
    {
        get; private set;
    }

    [ObservableProperty]
    public partial bool CanAbortLanding
    {
        get; private set;
    }

    [ObservableProperty]
    public partial bool IsAbortLandingVisible
    {
        get; private set;
    }

    [ObservableProperty]
    public partial string AbortLandingReason { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMissionOperationPending
    {
        get; private set;
    }

    [ObservableProperty] public partial double TargetSpeedMetersPerSecond { get; set; } = 5;
    [ObservableProperty] public partial VehicleSpeedTargetType SelectedSpeedTargetType { get; set; } = VehicleSpeedTargetType.GroundSpeed;
    [ObservableProperty] public partial IReadOnlyList<VehicleSpeedTargetType> SpeedTargetTypes { get; private set; } = [VehicleSpeedTargetType.GroundSpeed];
    [ObservableProperty]
    public partial bool IsSpeedTypeSelectorVisible
    {
        get; private set;
    }
    [ObservableProperty] public partial double TargetAltitudeAboveHomeMeters { get; set; } = 10;
    [ObservableProperty] public partial double LoiterRadiusMagnitudeMeters { get; set; } = 50;
    [ObservableProperty]
    public partial bool CanChangeSpeed
    {
        get; private set;
    }
    [ObservableProperty]
    public partial bool CanChangeAltitude
    {
        get; private set;
    }
    [ObservableProperty]
    public partial bool CanSetLoiterRadius
    {
        get; private set;
    }
    [ObservableProperty] public partial string ChangeAltitudeReason { get; private set; } = string.Empty;
    [ObservableProperty] public partial string LoiterRadiusReason { get; private set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsAdjustmentPending
    {
        get; private set;
    }

    /// <summary>Gets whether vehicle-changing controls may transmit in the current data-source mode.</summary>
    [ObservableProperty]
    public partial bool CanTransmit { get; private set; } = true;

    /// <summary>Gets the explicit live, simulation, or replay safety label.</summary>
    [ObservableProperty]
    public partial string DataSourceMode { get; private set; } = "LIVE / SIMULATION";

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        base.Dispose();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        altitudeReferenceService.Changed += OnAltitudeReferenceChanged;
        missionSnapshots.Changed += OnMissionSnapshotsChanged;
        parameterRegistry.Changed += OnVehicleParameterChanged;
        stateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        ApplySnapshot(activeVehicle.Current);
        if (replaySessionManager is not null)
        {
            replaySessionManager.Changed += OnReplayChanged;
            ApplyReplayState(replaySessionManager.Snapshot);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        replaySessionManager?.Changed -= OnReplayChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        altitudeReferenceService.Changed -= OnAltitudeReferenceChanged;
        missionSnapshots.Changed -= OnMissionSnapshotsChanged;
        parameterRegistry.Changed -= OnVehicleParameterChanged;
        stateSubscription?.Dispose();
        stateSubscription = null;
    }

    [RelayCommand]
    private Task ArmAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Arm", VehicleAction.Arm, (id, _, token) => commandService.ArmAsync(id, token), state => state.IsArmed, cancellationToken);
    }

    [RelayCommand]
    private Task DisarmAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Disarm", VehicleAction.Disarm, (id, confirmed, token) => commandService.DisarmAsync(id, confirmed, token), state => !state.IsArmed, cancellationToken);
    }

    [RelayCommand]
    private Task SetModeAsync(CancellationToken cancellationToken)
    {
        if (SelectedMode is null)
        {
            OperationState = AsyncOperationState.Warning("Select a flight mode first.");
            return Task.CompletedTask;
        }

        var selected = SelectedMode;
        return ExecuteAsync($"Set mode {selected.Name}", VehicleAction.SetMode,
            (id, _, token) => commandService.SetModeAsync(id, selected, token),
            state => state.CustomMode == selected.CustomMode,
            cancellationToken);
    }

    [RelayCommand]
    private Task TakeoffAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync($"Take off to {TakeoffAltitudeMeters:0.#} m", VehicleAction.Takeoff,
            (id, confirmed, token) => commandService.TakeoffAsync(id, TakeoffAltitudeMeters, confirmed, token),
            state => state.Flight.LandedState is VehicleLandedState.TakingOff or VehicleLandedState.InAir,
            cancellationToken);
    }

    [RelayCommand]
    private Task LandAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Land", VehicleAction.Land,
            (id, _, token) => commandService.LandAsync(id, token),
            state => state.Flight.LandedState is VehicleLandedState.Landing or VehicleLandedState.OnGround,
            cancellationToken);
    }

    [RelayCommand]
    private Task ReturnToLaunchAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Return to launch", VehicleAction.ReturnToLaunch,
            (id, _, token) => commandService.ReturnToLaunchAsync(id, token),
            state => modeCatalog.Find(state.Identity.Firmware.Family, VehicleMode.Rtl)?.CustomMode == state.CustomMode,
            cancellationToken);
    }

    [RelayCommand]
    private Task HoldAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Loiter / hold", VehicleAction.Hold,
            (id, _, token) => commandService.HoldAsync(id, token),
            state => modeCatalog.Find(state.Identity.Firmware.Family, VehicleMode.Loiter)?.CustomMode == state.CustomMode,
            cancellationToken);
    }

    [RelayCommand]
    private Task RebootAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Reboot autopilot", VehicleAction.RebootAutopilot,
            (id, confirmed, token) => commandService.RebootAutopilotAsync(id, confirmed, token), null, cancellationToken);
    }

    [RelayCommand]
    private Task SetHomeHereAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync("Set home here", VehicleAction.SetHomeHere,
            (id, confirmed, token) => commandService.SetHomeHereAsync(id, confirmed, token), null, cancellationToken);
    }

    [RelayCommand]
    private async Task ToggleAltitudeZeroAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.State is not { } state)
        {
            OperationState = AsyncOperationState.Disconnected();
            return;
        }

        string message;
        if (altitudeReferenceService.HasReference(state.VehicleId))
        {
            altitudeReferenceService.Reset(state.VehicleId);
            message = "Display altitude reference reset";
        }
        else if (state.Position.RelativeAltitudeMeters is { } altitude && altitudeReferenceService.TryZero(state.VehicleId, altitude))
        {
            message = "Display altitude zeroed";
        }
        else
        {
            OperationState = AsyncOperationState.Warning("Relative altitude is not currently available.");
            return;
        }

        OperationState = AsyncOperationState.Success(message);
        await notificationService.NotifyAsync(new UserNotification(message, VehicleId: state.VehicleId), cancellationToken);
        ApplySnapshot(activeVehicle.Current);
    }

    [RelayCommand]
    private Task SetCurrentMissionItemAsync(CancellationToken cancellationToken)
    {
        if (SelectedMissionSequence < 0 || SelectedMissionSequence > ushort.MaxValue || SelectedMissionSequence != Math.Truncate(SelectedMissionSequence))
        {
            OperationState = AsyncOperationState.Warning("Select a valid canonical mission sequence.");
            return Task.CompletedTask;
        }
        var sequence = (ushort)SelectedMissionSequence;
        return ExecuteMissionInterventionAsync("Set Current WP",
            (id, token) => missionInterventionService.SetCurrentMissionItemAsync(id, sequence, token),
            VehicleAction.SetCurrentMissionItem, sequence, false, cancellationToken);
    }

    [RelayCommand]
    private Task RestartMissionAsync(CancellationToken cancellationToken)
    {
        return ExecuteMissionInterventionAsync("Restart Mission", missionInterventionService.RestartMissionAsync,
            VehicleAction.RestartMission, null, true, cancellationToken);
    }

    [RelayCommand]
    private Task ResumeMissionAsync(CancellationToken cancellationToken)
    {
        return ExecuteMissionInterventionAsync("Resume Mission", missionInterventionService.ResumeMissionAsync,
            VehicleAction.ResumeMission, null, false, cancellationToken);
    }

    [RelayCommand]
    private Task AbortLandingAsync(CancellationToken cancellationToken)
    {
        return ExecuteMissionInterventionAsync("Abort Landing", missionInterventionService.AbortLandingAsync,
            VehicleAction.AbortLanding, null, true, cancellationToken);
    }

    private async Task ExecuteMissionInterventionAsync(
        string label,
        Func<VehicleId, CancellationToken, Task<MissionInterventionResult>> execute,
        VehicleAction action,
        ushort? sequence,
        bool confirm,
        CancellationToken cancellationToken)
    {
        if (IsMissionOperationPending)
        {
            OperationState = AsyncOperationState.Warning("Another mission operation is pending.");
            return;
        }
        if (activeVehicle.State is not { } state)
        {
            OperationState = AsyncOperationState.Disconnected();
            return;
        }
        var decision = missionInterventionService.Evaluate(state, action, sequence);
        if (!decision.IsAllowed)
        {
            OperationState = AsyncOperationState.Warning(decision.Reason ?? "Mission action is unavailable.");
            return;
        }
        if (confirm && !await confirmationService.ConfirmAsync(label, decision.Reason ?? $"Confirm {label}.", label, cancellationToken))
        {
            OperationState = AsyncOperationState.Warning($"{label} cancelled before transmission.");
            return;
        }

        IsMissionOperationPending = true;
        ApplySnapshot(activeVehicle.Current);
        PendingCommand = label;
        OperationState = AsyncOperationState.Busy($"{label}: awaiting command result");
        try
        {
            var result = await execute(state.VehicleId, cancellationToken);
            AckResult = result.Status.ToString();
            OperationState = result.Status switch
            {
                MissionInterventionStatus.TelemetryConfirmed => AsyncOperationState.Success(result.Message),
                MissionInterventionStatus.FallbackTelemetryConfirmed => AsyncOperationState.Success(result.Message),
                MissionInterventionStatus.AcceptedButNotTelemetryConfirmed => AsyncOperationState.Warning(result.Message),
                MissionInterventionStatus.Timeout => AsyncOperationState.Timeout(result.Message),
                MissionInterventionStatus.Busy or MissionInterventionStatus.Denied or MissionInterventionStatus.Unsupported => AsyncOperationState.Warning(result.Message),
                _ => AsyncOperationState.Error(result.Message)
            };
        }
        catch (OperationCanceledException)
        {
            OperationState = AsyncOperationState.Warning($"{label} cancelled.");
        }
        finally
        {
            IsMissionOperationPending = false;
            PendingCommand = null;
            ApplySnapshot(activeVehicle.Current);
        }
    }

    [RelayCommand]
    private Task ChangeSpeedAsync(CancellationToken cancellationToken)
    {
        return !double.IsFinite(TargetSpeedMetersPerSecond) || TargetSpeedMetersPerSecond <= 0
            ? InvalidAdjustment("Speed must be greater than 0.")
            : ExecuteAdjustmentAsync("Change Speed", (id, token) => adjustmentService.ChangeSpeedAsync(id, SelectedSpeedTargetType, TargetSpeedMetersPerSecond, token), cancellationToken);
    }

    [RelayCommand]
    private Task ChangeAltitudeAsync(CancellationToken cancellationToken)
    {
        return !double.IsFinite(TargetAltitudeAboveHomeMeters) || TargetAltitudeAboveHomeMeters < 0
            ? InvalidAdjustment("Target altitude above HOME must be non-negative.")
            : ExecuteAdjustmentAsync("Change Altitude", (id, token) => adjustmentService.SetGuidedAltitudeAsync(id, TargetAltitudeAboveHomeMeters, token), cancellationToken);
    }

    [RelayCommand]
    private Task SetLoiterRadiusAsync(CancellationToken cancellationToken)
    {
        return !double.IsFinite(LoiterRadiusMagnitudeMeters) || LoiterRadiusMagnitudeMeters <= 0
            ? InvalidAdjustment("Loiter radius must be greater than 0.")
            : ExecuteAdjustmentAsync("Set Loiter Radius", (id, token) => adjustmentService.SetLoiterRadiusAsync(id, LoiterRadiusMagnitudeMeters, token), cancellationToken);
    }

    private Task InvalidAdjustment(string message)
    {
        OperationState = AsyncOperationState.Warning(message);
        return Task.CompletedTask;
    }

    private async Task ExecuteAdjustmentAsync(string label, Func<VehicleId, CancellationToken, Task<VehicleAdjustmentResult>> execute, CancellationToken cancellationToken)
    {
        if (IsAdjustmentPending)
        {
            OperationState = AsyncOperationState.Warning("Another adjustment is pending.");
            return;
        }
        if (activeVehicle.State is not { } state)
        {
            OperationState = AsyncOperationState.Disconnected();
            return;
        }
        IsAdjustmentPending = true;
        ApplySnapshot(activeVehicle.Current);
        PendingCommand = label;
        OperationState = AsyncOperationState.Busy(label == "Set Loiter Radius" ? "Writing persistent parameter" : $"{label} pending");
        try
        {
            var result = await execute(state.VehicleId, cancellationToken);
            AckResult = result.Status.ToString();
            OperationState = result.Status switch
            {
                VehicleAdjustmentStatus.TelemetryConfirmed => AsyncOperationState.Success(result.Message),
                VehicleAdjustmentStatus.ParameterConfirmed => AsyncOperationState.Success(result.Message),
                VehicleAdjustmentStatus.CommandAccepted => AsyncOperationState.Success(result.Message),
                VehicleAdjustmentStatus.TargetSentButNotTelemetryConfirmed => AsyncOperationState.Warning(result.Message),
                VehicleAdjustmentStatus.Timeout => AsyncOperationState.Timeout(result.Message),
                VehicleAdjustmentStatus.Denied or VehicleAdjustmentStatus.Unsupported or VehicleAdjustmentStatus.Busy => AsyncOperationState.Warning(result.Message),
                _ => AsyncOperationState.Error(result.Message)
            };
        }
        catch (OperationCanceledException) { OperationState = AsyncOperationState.Warning($"{label} cancelled."); }
        finally
        {
            IsAdjustmentPending = false;
            PendingCommand = null;
            ApplySnapshot(activeVehicle.Current);
        }
    }

    [RelayCommand]
    private Task ExecuteExpertAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateExpertCommand(out var command, out var error))
        {
            OperationState = AsyncOperationState.Warning(error!);
            return Task.CompletedTask;
        }

        return ExecuteAsync($"Expert command {command!.CommandId}", VehicleAction.ExpertCommand,
            (_, confirmed, token) => commandService.ExecuteExpertAsync(command, confirmed, token), null, cancellationToken);
    }

    private async Task ExecuteAsync(
        string label,
        VehicleAction action,
        Func<VehicleId, bool, CancellationToken, Task<VehicleCommandResponse>> sendAsync,
        Func<VehicleState, bool>? observedPredicate,
        CancellationToken cancellationToken)
    {
        if (!CanTransmit)
        {
            OperationState = AsyncOperationState.Warning(
                "Vehicle commands are disabled while telemetry-log replay is loaded. Close the replay first.");
            return;
        }

        var state = activeVehicle.State;
        if (state is null)
        {
            OperationState = AsyncOperationState.Disconnected();
            return;
        }

        var decision = commandPolicy.Evaluate(state, action);
        if (!decision.IsAllowed)
        {
            OperationState = AsyncOperationState.Warning(decision.Reason ?? "Command denied by safety policy.");
            await notificationService.NotifyAsync(
                new UserNotification(
                    OperationState.Message!,
                    $"{label} denied",
                    UserNotificationSeverity.Warning,
                    VehicleId: state.VehicleId),
                cancellationToken);
            return;
        }

        var confirmed = false;
        if (decision.RequiresConfirmation)
        {
            try
            {
                confirmed = await confirmationService.ConfirmAsync(label, decision.Reason ?? "Confirm this action.", label, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                OperationState = activeVehicle.IsOnline
                    ? AsyncOperationState.Warning("Command cancelled before it was sent.")
                    : AsyncOperationState.Disconnected();
                return;
            }

            if (!confirmed)
            {
                OperationState = AsyncOperationState.Warning("Command cancelled before it was sent.");
                return;
            }
        }

        Logger.LogInformation("Starting vehicle action {Action} for {VehicleId}.", action, state.VehicleId);
        PendingCommand = label;
        OperationState = AsyncOperationState.Busy($"{label}: awaiting acknowledgement");
        AsyncOperationState final;
        try
        {
            final = await operationRunner.RunAsync(async (vehicleId, token) =>
                {
                    var response = await sendAsync(vehicleId, confirmed, token).ConfigureAwait(false);
                    Dispatcher.Dispatch(() => AckResult = $"{response.Result}: {response.Message}");
                    if (response.Result != VehicleCommandResult.Accepted)
                    {
                        await notificationService.NotifyAsync(
                            new UserNotification(
                                response.Message ?? $"{label} failed with {response.Result}.",
                                label,
                                response.Result is VehicleCommandResult.Timeout or VehicleCommandResult.Failed
                                    ? UserNotificationSeverity.Error
                                    : UserNotificationSeverity.Warning,
                                VehicleId: vehicleId),
                            token).ConfigureAwait(false);
                        return MapResponse(response);
                    }

                    if (observedPredicate is null)
                    {
                        return AsyncOperationState.Success($"{label} acknowledged by the vehicle.");
                    }

                    var observed = await WaitForObservedStateAsync(observedPredicate, token).ConfigureAwait(false);
                    return observed
                        ? AsyncOperationState.Success($"{label} acknowledged and confirmed by telemetry.")
                        : AsyncOperationState.Warning($"{label} was acknowledged, but telemetry has not confirmed the final state.");
                }, $"{label}: awaiting acknowledgement", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            final = AsyncOperationState.Warning("Command cancelled.");
        }

        Dispatcher.Dispatch(() =>
        {
            OperationState = final;
            PendingCommand = null;
        });
        Logger.LogInformation("Completed vehicle action {Action} for {VehicleId} with {Result}.", action, state.VehicleId, final.Status);
    }

    private async Task<bool> WaitForObservedStateAsync(Func<VehicleState, bool> predicate, CancellationToken cancellationToken)
    {
        if (activeVehicle.State is { } current && predicate(current))
        {
            return true;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handler(VehicleStateUpdated evt, CancellationToken eventCancellationToken)
        {
            if (evt.VehicleId == activeVehicle.VehicleId && predicate(evt.VehicleState))
            {
                completion.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        using var subscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(Handler);
        if (activeVehicle.State is { } latest && predicate(latest))
        {
            return true;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private bool TryCreateExpertCommand(out ExpertVehicleCommand? command, out string? error)
    {
        command = null;
        error = null;
        if (activeVehicle.VehicleId is not { } vehicleId || !ushort.TryParse(ExpertCommandId, NumberStyles.None, CultureInfo.InvariantCulture, out var commandId) || commandId == 0)
        {
            error = "Enter a command ID from 1 to 65535 while a vehicle is connected.";
            return false;
        }

        var values = ExpertParameters.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length != 7)
        {
            error = "Enter exactly seven expert parameters.";
            return false;
        }

        var parameters = new float[7];
        for (var index = 0; index < values.Length; index++)
        {
            if (!float.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out parameters[index]) || !float.IsFinite(parameters[index]))
            {
                error = $"Parameter {index + 1} is not a finite invariant-culture number.";
                return false;
            }
        }

        command = new ExpertVehicleCommand(vehicleId, commandId, parameters);
        return true;
    }

    private static AsyncOperationState MapResponse(VehicleCommandResponse response)
    {
        return response.Result switch
        {
            VehicleCommandResult.Timeout => AsyncOperationState.Timeout(response.Message ?? "Command acknowledgement timed out."),
            VehicleCommandResult.Busy => AsyncOperationState.Warning(response.Message ?? "Another command is pending."),
            VehicleCommandResult.TemporarilyRejected => AsyncOperationState.Warning(response.Message ?? "Command was temporarily rejected."),
            VehicleCommandResult.Denied or VehicleCommandResult.Unsupported or VehicleCommandResult.VehicleNotFound or VehicleCommandResult.Failed =>
                AsyncOperationState.Error(response.Message ?? $"Command failed with {response.Result}."),
            var _ => AsyncOperationState.Success(response.Message)
        };
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => ApplySnapshot(args.Current));
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId)
        {
            Dispatcher.Dispatch(() =>
            {
                if (evt.VehicleId == activeVehicle.VehicleId)
                {
                    ApplySnapshot(new ActiveVehicleSnapshot(evt.VehicleId, evt.VehicleState));
                }
            });
        }

        return Task.CompletedTask;
    }

    private void ApplySnapshot(ActiveVehicleSnapshot snapshot)
    {
        var state = snapshot.State;
        Modes = state is null ? [] : modeCatalog.GetModes(state.Identity.Firmware.Family);
        SelectedMode = Modes.FirstOrDefault(mode => mode.CustomMode == state?.CustomMode) ?? Modes.FirstOrDefault();
        ObservedState = state is null
            ? "No active vehicle"
            : $"{state.DisplayName}: {(state.IsArmed ? "Armed" : "Disarmed")}, {state.Flight.LandedState}, mode {state.CustomMode}";
        CanArm = CanTransmit && IsAllowed(state, VehicleAction.Arm);
        CanDisarm = CanTransmit && IsAllowed(state, VehicleAction.Disarm);
        CanTakeoff = CanTransmit && IsAllowed(state, VehicleAction.Takeoff);
        CanLand = CanTransmit && IsAllowed(state, VehicleAction.Land);
        CanHoldPosition = CanTransmit && IsAllowed(state, VehicleAction.Hold);
        CanReturnToLaunch = CanTransmit && IsAllowed(state, VehicleAction.ReturnToLaunch);
        CanReboot = CanTransmit && IsAllowed(state, VehicleAction.RebootAutopilot);
        CanSetHome = CanTransmit && IsAllowed(state, VehicleAction.SetHomeHere);
        var hasReference = state is not null && altitudeReferenceService.HasReference(state.VehicleId);
        AltitudeZeroActionText = hasReference ? "Reset Altitude" : "Zero Altitude";
        CanToggleAltitudeZero = state is not null && (hasReference || (state.Position.RelativeAltitudeMeters is { } altitude && double.IsFinite(altitude)));
        CurrentMissionSequenceText = state?.Navigation.CurrentMissionSequence is { } current ? $"Current sequence: {current}" : "Current sequence: unknown";
        var selectedSequence = SelectedMissionSequence is >= 0 and <= ushort.MaxValue && SelectedMissionSequence == Math.Truncate(SelectedMissionSequence)
            ? (ushort?)SelectedMissionSequence
            : null;
        CanSetCurrentMissionItem = !IsMissionOperationPending && state is not null && selectedSequence is { } selected && missionInterventionService.Evaluate(state, VehicleAction.SetCurrentMissionItem, selected).IsAllowed;
        CanRestartMission = !IsMissionOperationPending && state is not null && missionInterventionService.Evaluate(state, VehicleAction.RestartMission).IsAllowed;
        CanResumeMission = !IsMissionOperationPending && state is not null && missionInterventionService.Evaluate(state, VehicleAction.ResumeMission).IsAllowed;
        IsAbortLandingVisible = state?.Identity.Firmware.Family == MissionPlanner.Firmware.FirmwareFamily.ArduPlane;
        var abortDecision = state is null ? VehicleCommandDecision.Deny("No active vehicle.") : missionInterventionService.Evaluate(state, VehicleAction.AbortLanding);
        CanAbortLanding = !IsMissionOperationPending && IsAbortLandingVisible && abortDecision.IsAllowed;
        AbortLandingReason = abortDecision.Reason ?? "Abort the active landing approach.";
        SpeedTargetTypes = state?.Identity.Firmware.Family == MissionPlanner.Firmware.FirmwareFamily.ArduPlane
            ? [VehicleSpeedTargetType.Airspeed, VehicleSpeedTargetType.GroundSpeed]
            : [VehicleSpeedTargetType.GroundSpeed];
        IsSpeedTypeSelectorVisible = SpeedTargetTypes.Count > 1;
        if (!SpeedTargetTypes.Contains(SelectedSpeedTargetType))
        {
            SelectedSpeedTargetType = VehicleSpeedTargetType.GroundSpeed;
        }

        var speedDecision = state is null ? VehicleCommandDecision.Deny("No active vehicle.") : adjustmentService.EvaluateSpeed(state, SelectedSpeedTargetType);
        var altitudeDecision = state is null ? VehicleCommandDecision.Deny("No active vehicle.") : adjustmentService.EvaluateAltitude(state);
        var radiusDecision = state is null ? VehicleCommandDecision.Deny("No active vehicle.") : adjustmentService.EvaluateLoiterRadius(state);
        CanChangeSpeed = !IsAdjustmentPending && speedDecision.IsAllowed && double.IsFinite(TargetSpeedMetersPerSecond) && TargetSpeedMetersPerSecond > 0;
        CanChangeAltitude = !IsAdjustmentPending && altitudeDecision.IsAllowed && double.IsFinite(TargetAltitudeAboveHomeMeters) && TargetAltitudeAboveHomeMeters >= 0;
        CanSetLoiterRadius = !IsAdjustmentPending && radiusDecision.IsAllowed && double.IsFinite(LoiterRadiusMagnitudeMeters) && LoiterRadiusMagnitudeMeters > 0;
        ChangeAltitudeReason = altitudeDecision.Reason ?? "Absolute target altitude above HOME.";
        LoiterRadiusReason = radiusDecision.Reason ?? "Persistent vehicle parameter. Existing loiter direction is preserved.";
    }

    private void OnAltitudeReferenceChanged(object? sender, LocalAltitudeReferenceChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId)
        {
            Dispatcher.Dispatch(() => ApplySnapshot(activeVehicle.Current));
        }
    }

    private void OnMissionSnapshotsChanged(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(() => ApplySnapshot(activeVehicle.Current));
    }

    private void OnVehicleParameterChanged(VehicleParameterChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => ApplySnapshot(activeVehicle.Current));
    }

    partial void OnSelectedMissionSequenceChanged(double value) => ApplySnapshot(activeVehicle.Current);
    partial void OnSelectedSpeedTargetTypeChanged(VehicleSpeedTargetType value) => ApplySnapshot(activeVehicle.Current);
    partial void OnTargetSpeedMetersPerSecondChanged(double value) => ApplySnapshot(activeVehicle.Current);
    partial void OnTargetAltitudeAboveHomeMetersChanged(double value) => ApplySnapshot(activeVehicle.Current);
    partial void OnLoiterRadiusMagnitudeMetersChanged(double value) => ApplySnapshot(activeVehicle.Current);

    private void OnReplayChanged(ReplaySessionChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => ApplyReplayState(args.Snapshot));
    }

    private void ApplyReplayState(ReplaySessionSnapshot snapshot)
    {
        CanTransmit = !snapshot.IsTransmissionProhibited;
        DataSourceMode = CanTransmit ? "LIVE / SIMULATION" : "REPLAY · READ ONLY · ALL SENDS DISABLED";
        ApplySnapshot(activeVehicle.Current);
    }

    private bool IsAllowed(VehicleState? state, VehicleAction action)
    {
        return state is not null && commandPolicy.Evaluate(state, action).IsAllowed;
    }
}

