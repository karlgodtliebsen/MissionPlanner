using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.InitSetup.Sections;

/// <summary>Projects the Core accelerometer calibration state machine into guided Setup controls.</summary>
public sealed partial class AccelerometerSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IArduPilotCalibrationService calibration;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<AccelerometerSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the accelerometer Setup workflow.</summary>
    /// <param name="descriptor">The workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="calibration">The Core calibration state machine.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The Setup evidence store.</param>
    /// <param name="workflowCatalog">The Setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public AccelerometerSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IArduPilotCalibrationService calibration,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        ILogger<AccelerometerSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.calibration = calibration;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.clock = clock;
        this.dispatcher = dispatcher;
        this.logger = logger;
        calibration.StateChanged += OnCalibrationStateChanged;
        Show(calibration.Current);
    }

    /// <summary>Gets the current calibration workflow stage.</summary>
    [ObservableProperty]
    public partial CalibrationWorkflowState CalibrationState { get; private set; }

    /// <summary>Gets the primary physical instruction.</summary>
    [ObservableProperty]
    public partial string Instruction { get; private set; } = string.Empty;

    /// <summary>Gets supplemental ArduPilot status text.</summary>
    [ObservableProperty]
    public partial string? SupplementalStatus { get; private set; }

    /// <summary>Gets the current orientation label.</summary>
    [ObservableProperty]
    public partial string Orientation { get; private set; } = "No orientation requested";

    /// <summary>Gets the repository image illustrating the requested orientation.</summary>
    [ObservableProperty]
    public partial string OrientationImage { get; private set; } = "x_calibration01_x.jpg";

    /// <summary>Gets a concise completed-orientation summary.</summary>
    [ObservableProperty]
    public partial string CompletedOrientations { get; private set; } = "0 of 6 positions sampled";

    /// <summary>Gets whether the vehicle is waiting for the user to confirm placement.</summary>
    public bool CanConfirmOrientation => CalibrationState == CalibrationWorkflowState.WaitingForOrientation && calibration.Current.RequiredOrientation is not null;

    /// <summary>Gets whether no calibration operation is active.</summary>
    public bool CanStart => CalibrationState is CalibrationWorkflowState.NotStarted or CalibrationWorkflowState.Success or
        CalibrationWorkflowState.Failed or CalibrationWorkflowState.Cancelled or CalibrationWorkflowState.Disconnected;

    /// <summary>Gets whether an active operation can be cancelled.</summary>
    public bool CanCancel => CalibrationState is CalibrationWorkflowState.Preparing or CalibrationWorkflowState.WaitingForOrientation or
        CalibrationWorkflowState.Sampling or CalibrationWorkflowState.Completing;

    /// <inheritdoc />
    public override void Cancel()
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
    public override void Dispose()
    {
        calibration.StateChanged -= OnCalibrationStateChanged;
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
            logger.LogError(exception, "Confirming calibration orientation failed.");
            Error = exception.Message;
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
            logger.LogError(exception, "Cancelling accelerometer calibration failed.");
            Error = exception.Message;
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
            Error = "Connect a vehicle before starting calibration.";
            return;
        }

        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        try
        {
            await operation(vehicleId, operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Accelerometer Setup operation failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private void OnCalibrationStateChanged(object? sender, CalibrationStateChangedEventArgs args)
    {
        dispatcher.Dispatch(() => Show(args.Snapshot));
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
        Error = snapshot.FailureReason;
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
