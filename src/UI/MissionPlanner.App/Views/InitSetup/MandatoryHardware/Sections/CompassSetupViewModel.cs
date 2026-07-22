using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects compass discovery, editing, and the onboard calibration state machine into Setup controls.</summary>
public sealed partial class CompassSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ICompassConfigurationService compassService;
    private readonly IArduPilotCompassCalibrationService calibration;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<CompassSetupViewModel> logger;
    private IReadOnlyList<CompassOrientationOption> orientationOptions = [];
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the compass Setup workflow.</summary>
    /// <param name="descriptor">The compass workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="compassService">The compass discovery and configuration service.</param>
    /// <param name="calibration">The onboard compass calibration state machine.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The Setup evidence store.</param>
    /// <param name="workflowCatalog">The Setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public CompassSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        ICompassConfigurationService compassService,
        IArduPilotCompassCalibrationService calibration,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        ILogger<CompassSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.compassService = compassService;
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

    /// <summary>Gets the discovered compass instances.</summary>
    public ObservableCollection<CompassInstanceViewModel> Compasses { get; } = [];

    /// <summary>Gets detected duplicate-identity or priority inconsistencies.</summary>
    public ObservableCollection<string> Issues { get; } = [];

    /// <summary>Gets the inventory status or the latest operation result.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Load the connected vehicle's compass configuration.";

    /// <summary>Gets the current calibration workflow stage.</summary>
    [ObservableProperty]
    public partial CompassCalibrationWorkflowState CalibrationState { get; private set; }

    /// <summary>Gets the primary calibration instruction.</summary>
    [ObservableProperty]
    public partial string Instruction { get; private set; } = string.Empty;

    /// <summary>Gets the per-compass progress summary.</summary>
    [ObservableProperty]
    public partial string ProgressSummary { get; private set; } = string.Empty;

    /// <summary>Gets the post-calibration quality summary, when available.</summary>
    [ObservableProperty]
    public partial string? QualitySummary { get; private set; }

    /// <summary>Gets whether at least one compass was discovered.</summary>
    public bool HasCompasses => Compasses.Count > 0;

    /// <summary>Gets whether the inventory reported any configuration issues.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets whether calibration results are awaiting explicit acceptance.</summary>
    public bool CanAccept => CalibrationState == CompassCalibrationWorkflowState.PendingAcceptance;

    /// <summary>Gets whether calibration can be started.</summary>
    public bool CanStart => CalibrationState is CompassCalibrationWorkflowState.NotStarted or CompassCalibrationWorkflowState.Success or
        CompassCalibrationWorkflowState.Failed or CompassCalibrationWorkflowState.Cancelled or CompassCalibrationWorkflowState.Disconnected;

    /// <summary>Gets whether an active calibration can be cancelled.</summary>
    public bool CanCancel => CalibrationState is CompassCalibrationWorkflowState.Preparing or CompassCalibrationWorkflowState.Running or
        CompassCalibrationWorkflowState.PendingAcceptance;

    /// <summary>Loads the compass inventory for the active vehicle.</summary>
    /// <returns>A task that completes after the inventory is projected.</returns>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before loading compass configuration.";
            return;
        }

        var token = StartOperation();
        try
        {
            var inventory = await compassService.GetInventoryAsync(vehicleId, token);
            dispatcher.Dispatch(() => ShowInventory(inventory));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading compass configuration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

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

    /// <summary>Applies the reviewed edits for one compass with readback confirmation.</summary>
    /// <param name="item">The compass row to apply.</param>
    /// <returns>A task that completes after the writes are confirmed or reported failed.</returns>
    internal async Task ApplyCompassAsync(CompassInstanceViewModel item)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before editing compass parameters.";
            return;
        }

        if (!item.IsUsed && WouldDisableLastEnabledCompass(item))
        {
            var accepted = await confirmation.ConfirmAsync(
                "Disable compass",
                $"Compass {item.Index} is the only enabled compass. Disabling it removes heading estimation from magnetometers. Continue?",
                "Disable compass");
            if (!accepted)
            {
                item.RevertUse();
                return;
            }
        }

        var token = StartOperation();
        var messages = new List<string>();
        try
        {
            if (item.SelectedOrientation is { } orientation && orientation.Value != item.Instance.Orientation)
            {
                messages.Add((await compassService.SetOrientationAsync(vehicleId, item.Index, orientation.Value, token)).Message);
            }

            if (item.IsUsed != item.Instance.Use)
            {
                messages.Add((await compassService.SetUseAsync(vehicleId, item.Index, item.IsUsed, token)).Message);
            }

            if (item.SupportsExternal && item.Instance.External is { } external && item.IsExternal != external)
            {
                messages.Add((await compassService.SetExternalAsync(vehicleId, item.Index, item.IsExternal, token)).Message);
            }

            Status = messages.Count == 0 ? "No compass changes were pending." : string.Join(Environment.NewLine, messages);
            var inventory = await compassService.GetInventoryAsync(vehicleId, token);
            dispatcher.Dispatch(() => ShowInventory(inventory, true));
        }
        catch (OperationCanceledException)
        {
            Status = "Compass edit was cancelled. Refresh values before continuing.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying compass edits failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private Task LoadInventoryAsync()
    {
        return LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshInventoryAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Reconnect the vehicle before refreshing compass values.";
            return;
        }

        var token = StartOperation();
        try
        {
            await compassService.RefreshAsync(vehicleId, token);
            var inventory = await compassService.GetInventoryAsync(vehicleId, token);
            dispatcher.Dispatch(() => ShowInventory(inventory));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Refreshing compass configuration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private bool CanStartCommand()
    {
        return CanStart && activeVehicle.IsOnline;
    }

    [RelayCommand(CanExecute = nameof(CanStartCommand))]
    private async Task StartCalibrationAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Error = "Connect a vehicle before starting compass calibration.";
            return;
        }

        var accepted = await confirmation.ConfirmAsync(
            "Start compass calibration",
            "Move away from metal structures, vehicles, and magnetic interference. You will rotate the vehicle through all orientations. Continue?",
            "Start calibration");
        if (!accepted)
        {
            return;
        }

        var token = StartOperation();
        try
        {
            await calibration.StartAsync(vehicleId, false, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Starting compass calibration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private bool CanAcceptCommand()
    {
        return CanAccept;
    }

    [RelayCommand(CanExecute = nameof(CanAcceptCommand))]
    private async Task AcceptCalibrationAsync()
    {
        try
        {
            await calibration.AcceptAsync(activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Accepting compass calibration failed.");
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
            logger.LogError(exception, "Cancelling compass calibration failed.");
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        calibration.Reset();
    }

    private bool WouldDisableLastEnabledCompass(CompassInstanceViewModel item)
    {
        var inventory = new CompassInventory(
            activeVehicle.VehicleId ?? default,
            Compasses.Select(compass => compass.Instance).ToArray(),
            orientationOptions,
            []);
        return compassService.WouldDisableOnlyEnabledCompass(inventory, item.Index);
    }

    private CancellationToken StartOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        return operationCancellation.Token;
    }

    private void ShowInventory(CompassInventory inventory, bool preserveStatus = false)
    {
        orientationOptions = inventory.OrientationOptions;
        Compasses.Clear();
        foreach (var compass in inventory.Compasses)
        {
            Compasses.Add(new CompassInstanceViewModel(compass, inventory.OrientationOptions, this));
        }

        Issues.Clear();
        foreach (var issue in inventory.Issues)
        {
            Issues.Add(issue.Message);
        }

        if (!preserveStatus)
        {
            Status = Compasses.Count == 0
                ? "No compass devices were detected. Connect or re-detect compasses, then refresh."
                : "Review compass identity and orientation, or start guided calibration.";
        }

        OnPropertyChanged(nameof(HasCompasses));
        OnPropertyChanged(nameof(HasIssues));
    }

    private void OnCalibrationStateChanged(object? sender, CompassCalibrationStateChangedEventArgs args)
    {
        dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void Show(CompassCalibrationSnapshot snapshot)
    {
        CalibrationState = snapshot.State;
        Instruction = snapshot.Instruction;
        Progress = snapshot.OverallProgress;
        QualitySummary = snapshot.QualitySummary;
        ProgressSummary = snapshot.Progress.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, snapshot.Progress.Select(item =>
                $"Compass {item.CompassId + 1}: {item.Status} ({item.CompletionPercent}%)"));
        Error = snapshot.FailureReason;
        OnPropertyChanged(nameof(CanAccept));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanCancel));
        StartCalibrationCommand.NotifyCanExecuteChanged();
        AcceptCalibrationCommand.NotifyCanExecuteChanged();
        CancelCalibrationCommand.NotifyCanExecuteChanged();

        if (snapshot.State == CompassCalibrationWorkflowState.Success && snapshot.VehicleId is { } vehicleId &&
            activeVehicle.State is { } state && state.VehicleId == vehicleId)
        {
            completionStore.Save(workflowCatalog.CreateEvidence(
                SetupWorkflowKey.Compass,
                state,
                parameterRegistry.GetAllParameters(vehicleId),
                clock.UtcNow));
            logger.LogInformation("Recorded confirmed compass setup evidence for {VehicleId}.", vehicleId);
        }
    }
}
