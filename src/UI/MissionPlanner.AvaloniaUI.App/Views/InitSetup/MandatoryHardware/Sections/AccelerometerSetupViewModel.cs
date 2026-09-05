using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects the Core accelerometer calibration state machine into guided Setup controls.</summary>
public sealed partial class AccelerometerSetupViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IArduPilotCalibrationService calibration;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the accelerometer Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="calibration">The Core calibration state machine.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The Setup evidence store.</param>
    /// <param name="workflowCatalog">The Setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public AccelerometerSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IArduPilotCalibrationService calibration,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock, ILogger<AccelerometerSetupViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.calibration = calibration;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.clock = clock;
    }

    /// <summary>Gets the current calibration workflow stage.</summary>
    [ObservableProperty]
    public partial CalibrationWorkflowState CalibrationState
    {
        get;
        private set;
    }

    /// <summary>Gets the primary physical instruction.</summary>
    [ObservableProperty]
    public partial string Instruction
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets supplemental ArduPilot status text.</summary>
    [ObservableProperty]
    public partial string? SupplementalStatus
    {
        get;
        private set;
    }

    /// <summary>Gets the current orientation label.</summary>
    [ObservableProperty]
    public partial string Orientation
    {
        get;
        private set;
    } = "No orientation requested";

    /// <summary>Gets the repository image illustrating the requested orientation.</summary>
    [ObservableProperty]
    public partial string OrientationImage
    {
        get;
        private set;
    } = "x_calibration01_x.jpg";

    /// <summary>Gets a concise completed-orientation summary.</summary>
    [ObservableProperty]
    public partial string CompletedOrientations
    {
        get;
        private set;
    } = "0 of 6 positions sampled";

    /// <summary>Gets whether the vehicle is waiting for the user to confirm placement.</summary>
    public bool CanConfirmOrientation => CalibrationState == CalibrationWorkflowState.WaitingForOrientation && calibration.Current.RequiredOrientation is not null;

    /// <summary>Gets whether no calibration operation is active.</summary>
    public bool CanStart => CalibrationState is CalibrationWorkflowState.NotStarted or CalibrationWorkflowState.Success or
        CalibrationWorkflowState.Failed or CalibrationWorkflowState.Cancelled or CalibrationWorkflowState.Disconnected;

    /// <summary>Gets whether an active operation can be cancelled.</summary>
    public bool CanCancel => CalibrationState is CalibrationWorkflowState.Preparing or CalibrationWorkflowState.WaitingForOrientation or
        CalibrationWorkflowState.Sampling or CalibrationWorkflowState.Completing;

    /// <inheritdoc />
    public void Cancel()
    {
        if (CanCancel)
        {
            _ = calibration.CancelAsync();
        }

        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        calibration.StateChanged += OnCalibrationStateChanged;
        activeVehicle.Changed += OnActiveVehicleChanged;
        Show(calibration.Current);
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        calibration.StateChanged -= OnCalibrationStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        calibration.Dispose();
        base.Dispose();
    }

    private bool CanStartCommand()
    {
        return CanStart && activeVehicle.IsOnline;
    }

    [RelayCommand(CanExecute = nameof(CanStartCommand))]
    private async Task StartSixPositionAsync()
    {
        if (!await ConfirmSafeStartAsync("six-position accelerometer calibration"))
        {
            return;
        }

        await RunAsync((vehicleId, token) => calibration.StartSixPositionAsync(vehicleId, token));
    }

    [RelayCommand(CanExecute = nameof(CanStartCommand))]
    private async Task StartLevelAsync()
    {
        if (!await ConfirmSafeStartAsync("level calibration"))
        {
            return;
        }

        await RunAsync((vehicleId, token) => calibration.StartLevelAsync(vehicleId, token));
    }

    private bool CanConfirm()
    {
        return CanConfirmOrientation;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmOrientationAsync()
    {
        try
        {
            await calibration.ConfirmOrientationAsync(activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Confirming calibration orientation failed.");
            SetMessages(exception);
        }
    }

    private bool CanCancelCommand()
    {
        return CanCancel;
    }

    [RelayCommand(CanExecute = nameof(CanCancelCommand))]
    private async Task CancelCalibrationAsync()
    {
        try
        {
            await calibration.CancelAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Cancelling accelerometer calibration failed.");
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        calibration.Reset();
    }

    private async Task<bool> ConfirmSafeStartAsync(string operation)
    {
        return activeVehicle.IsOnline && await confirmation.ConfirmAsync(
            "Start calibration",
            $"Remove propellers, keep the vehicle disarmed, and place it on a stable surface before starting {operation}.",
            "Start calibration");
    }

    private async Task RunAsync(Func<VehicleId, CancellationToken, Task> operation)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetMessages(null, "Connect a vehicle before starting calibration.");
            return;
        }

        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetMessages(null, null);
        try
        {
            await operation(vehicleId, operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Accelerometer Setup operation failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
    }

    private void OnCalibrationStateChanged(CalibrationStateChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (!SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            return;
        }

        Dispatcher.Dispatch(() => Show(calibration.Current));
    }

    private void Show(CalibrationSnapshot snapshot)
    {
        CalibrationState = snapshot.State;
        Instruction = snapshot.Instruction;
        SupplementalStatus = snapshot.SupplementalStatus;
        Progress = snapshot.Progress;
        Orientation = snapshot.RequiredOrientation?.ToString() ?? "No orientation requested";
        OrientationImage = ImageFor(snapshot.RequiredOrientation);
        CompletedOrientations = $"{snapshot.CompletedOrientations.Count} of 6 positions sampled";
        SetMessages(null, snapshot.FailureReason);
        OnPropertyChanged(nameof(CanConfirmOrientation));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanCancel));
        StartSixPositionCommand.NotifyCanExecuteChanged();
        StartLevelCommand.NotifyCanExecuteChanged();
        ConfirmOrientationCommand.NotifyCanExecuteChanged();
        CancelCalibrationCommand.NotifyCanExecuteChanged();

        if (snapshot.State == CalibrationWorkflowState.Success && snapshot.VehicleId is { } vehicleId &&
            activeVehicle.State is { } state && state.VehicleId == vehicleId)
        {
            completionStore.Save(workflowCatalog.CreateEvidence(
                SetupWorkflowKey.Accelerometer,
                state,
                parameterRegistry.GetAllParameters(vehicleId),
                clock.UtcNow));
        }
    }

    private static string ImageFor(CalibrationOrientation? orientation)
    {
        return orientation switch
        {
            CalibrationOrientation.Level => "x_calibration02_x.jpg",
            CalibrationOrientation.Left => "x_calibration04_x.jpg",
            CalibrationOrientation.Right => "x_calibration06_x.jpg",
            CalibrationOrientation.NoseDown => "x_calibration05_x.jpg",
            CalibrationOrientation.NoseUp => "x_calibration07_x.jpg",
            CalibrationOrientation.Back => "x_calibration03_x.jpg",
            var _ => "x_calibration01_x.jpg"
        };
    }
}

