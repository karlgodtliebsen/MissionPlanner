using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Presents safety-aware, acknowledged actions for the active ArduPilot vehicle.
/// </summary>
public partial class ActionsTabViewModel : ObservableObject, IFlightDataTabLifecycle, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleCommandService commandService;
    private readonly IVehicleCommandPolicy commandPolicy;
    private readonly IArduPilotModeCatalog modeCatalog;
    private readonly IUserConfirmationService confirmationService;
    private readonly IDispatcher dispatcher;
    private readonly AsyncOperationRunner operationRunner;
    private readonly ILogger<ActionsTabViewModel> logger;
    private readonly FlightDataTabLifecycle lifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionsTabViewModel"/> class.
    /// </summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="commandService">The acknowledged vehicle command service.</param>
    /// <param name="commandPolicy">The vehicle safety policy.</param>
    /// <param name="modeCatalog">The firmware-specific mode catalog.</param>
    /// <param name="confirmationService">The hazardous-action confirmation service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public ActionsTabViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleCommandService commandService,
        IVehicleCommandPolicy commandPolicy,
        IArduPilotModeCatalog modeCatalog,
        IUserConfirmationService confirmationService,
        IDispatcher dispatcher,
        ILogger<ActionsTabViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.commandService = commandService;
        this.commandPolicy = commandPolicy;
        this.modeCatalog = modeCatalog;
        this.confirmationService = confirmationService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        operationRunner = new AsyncOperationRunner(activeVehicle);
        lifecycle = new FlightDataTabLifecycle("Actions", activeVehicle, startAsync: _ =>
        {
            activeVehicle.Changed += OnActiveVehicleChanged;
            ApplySnapshot(activeVehicle.Current);
            return Task.FromResult<IDisposable?>(new CallbackDisposable(() => activeVehicle.Changed -= OnActiveVehicleChanged));
        });
        ApplySnapshot(activeVehicle.Current);
    }

    /// <inheritdoc />
    public string Key => lifecycle.Key;

    /// <inheritdoc />
    public bool IsActive => lifecycle.IsActive;

    /// <inheritdoc />
    public bool IsInitialized => lifecycle.IsInitialized;

    /// <summary>Gets the modes appropriate to the connected firmware family.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<VehicleModeOption> Modes { get; private set; } = [];

    /// <summary>Gets or sets the selected family-specific flight mode.</summary>
    [ObservableProperty]
    public partial VehicleModeOption? SelectedMode { get; set; }

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
    public partial bool IsExpertSectionVisible { get; set; }

    /// <summary>Gets the command currently awaiting acknowledgement or telemetry confirmation.</summary>
    [ObservableProperty]
    public partial string? PendingCommand { get; private set; }

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
    public partial bool CanArm { get; private set; }

    /// <summary>Gets whether disarm is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanDisarm { get; private set; }

    /// <summary>Gets whether takeoff is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanTakeoff { get; private set; }

    /// <summary>Gets whether in-flight recovery actions are currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanInFlightAction { get; private set; }

    /// <summary>Gets whether reboot is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanReboot { get; private set; }

    /// <summary>Gets whether setting home is currently permitted by policy.</summary>
    [ObservableProperty]
    public partial bool CanSetHome { get; private set; }

    /// <inheritdoc />
    public Task ActivateAsync(CancellationToken cancellationToken = default) => lifecycle.ActivateAsync(cancellationToken);

    /// <inheritdoc />
    public Task DeactivateAsync() => lifecycle.DeactivateAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        lifecycle.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [RelayCommand]
    private Task ArmAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Arm", VehicleAction.Arm, (id, _, token) => commandService.ArmAsync(id, token), state => state.IsArmed, cancellationToken);

    [RelayCommand]
    private Task DisarmAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Disarm", VehicleAction.Disarm, (id, confirmed, token) => commandService.DisarmAsync(id, confirmed, token), state => !state.IsArmed, cancellationToken);

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
    private Task TakeoffAsync(CancellationToken cancellationToken) =>
        ExecuteAsync($"Take off to {TakeoffAltitudeMeters:0.#} m", VehicleAction.Takeoff,
            (id, confirmed, token) => commandService.TakeoffAsync(id, TakeoffAltitudeMeters, confirmed, token),
            state => state.Flight.LandedState is VehicleLandedState.TakingOff or VehicleLandedState.InAir,
            cancellationToken);

    [RelayCommand]
    private Task LandAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Land", VehicleAction.Land,
            (id, _, token) => commandService.LandAsync(id, token),
            state => state.Flight.LandedState is VehicleLandedState.Landing or VehicleLandedState.OnGround,
            cancellationToken);

    [RelayCommand]
    private Task ReturnToLaunchAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Return to launch", VehicleAction.ReturnToLaunch,
            (id, _, token) => commandService.ReturnToLaunchAsync(id, token),
            state => modeCatalog.Find(state.Identity.Firmware.Family, VehicleMode.Rtl)?.CustomMode == state.CustomMode,
            cancellationToken);

    [RelayCommand]
    private Task HoldAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Loiter / hold", VehicleAction.Hold,
            (id, _, token) => commandService.HoldAsync(id, token),
            state => modeCatalog.Find(state.Identity.Firmware.Family, VehicleMode.Loiter)?.CustomMode == state.CustomMode,
            cancellationToken);

    [RelayCommand]
    private Task RebootAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Reboot autopilot", VehicleAction.RebootAutopilot,
            (id, confirmed, token) => commandService.RebootAutopilotAsync(id, confirmed, token), null, cancellationToken);

    [RelayCommand]
    private Task SetHomeHereAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("Set home here", VehicleAction.SetHomeHere,
            (id, confirmed, token) => commandService.SetHomeHereAsync(id, confirmed, token), null, cancellationToken);

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

        logger.LogInformation("Starting vehicle action {Action} for {VehicleId}.", action, state.VehicleId);
        PendingCommand = label;
        OperationState = AsyncOperationState.Busy($"{label}: awaiting acknowledgement");
        AsyncOperationState final;
        try
        {
            final = await operationRunner.RunAsync(async (vehicleId, token) =>
            {
                var response = await sendAsync(vehicleId, confirmed, token).ConfigureAwait(false);
                dispatcher.Dispatch(() => AckResult = $"{response.Result}: {response.Message}");
                if (response.Result != VehicleCommandResult.Accepted)
                {
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

        dispatcher.Dispatch(() =>
        {
            OperationState = final;
            PendingCommand = null;
        });
        logger.LogInformation("Completed vehicle action {Action} for {VehicleId} with {Result}.", action, state.VehicleId, final.Status);
    }

    private async Task<bool> WaitForObservedStateAsync(Func<VehicleState, bool> predicate, CancellationToken cancellationToken)
    {
        if (activeVehicle.State is { } current && predicate(current))
        {
            return true;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, ActiveVehicleChangedEventArgs args)
        {
            if (args.Current.State is { } updated && predicate(updated))
            {
                completion.TrySetResult(true);
            }
        }

        activeVehicle.Changed += Handler;
        try
        {
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
        finally
        {
            activeVehicle.Changed -= Handler;
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

    private static AsyncOperationState MapResponse(VehicleCommandResponse response) => response.Result switch
    {
        VehicleCommandResult.Timeout => AsyncOperationState.Timeout(response.Message ?? "Command acknowledgement timed out."),
        VehicleCommandResult.Busy => AsyncOperationState.Warning(response.Message ?? "Another command is pending."),
        VehicleCommandResult.TemporarilyRejected => AsyncOperationState.Warning(response.Message ?? "Command was temporarily rejected."),
        VehicleCommandResult.Denied or VehicleCommandResult.Unsupported or VehicleCommandResult.VehicleNotFound or VehicleCommandResult.Failed =>
            AsyncOperationState.Error(response.Message ?? $"Command failed with {response.Result}."),
        _ => AsyncOperationState.Success(response.Message)
    };

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args) =>
        dispatcher.Dispatch(() => ApplySnapshot(args.Current));

    private void ApplySnapshot(ActiveVehicleSnapshot snapshot)
    {
        var state = snapshot.State;
        Modes = state is null ? [] : modeCatalog.GetModes(state.Identity.Firmware.Family);
        SelectedMode = Modes.FirstOrDefault(mode => mode.CustomMode == state?.CustomMode) ?? Modes.FirstOrDefault();
        ObservedState = state is null
            ? "No active vehicle"
            : $"{state.DisplayName}: {(state.IsArmed ? "Armed" : "Disarmed")}, {state.Flight.LandedState}, mode {state.CustomMode}";
        CanArm = IsAllowed(state, VehicleAction.Arm);
        CanDisarm = IsAllowed(state, VehicleAction.Disarm);
        CanTakeoff = IsAllowed(state, VehicleAction.Takeoff);
        CanInFlightAction = IsAllowed(state, VehicleAction.Land);
        CanReboot = IsAllowed(state, VehicleAction.RebootAutopilot);
        CanSetHome = IsAllowed(state, VehicleAction.SetHomeHere);
    }

    private bool IsAllowed(VehicleState? state, VehicleAction action) => state is not null && commandPolicy.Evaluate(state, action).IsAllowed;

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                callback();
            }
        }
    }
}
