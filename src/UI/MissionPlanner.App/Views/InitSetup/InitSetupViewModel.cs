using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.Tabs;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>Presents the vehicle-aware initial-setup workflow shell and shared operation state.</summary>
public partial class InitSetupViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupWorkflowCatalog catalog;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowViewModelFactory workflowFactory;
    private readonly ISetupNavigationService navigation;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<InitSetupViewModel> logger;
    private readonly Dictionary<SetupWorkflowKey, Tabs.SetupWorkflowDetailViewModel> workflowViewModels = [];
    private readonly object parameterRefreshSync = new();
    private CancellationTokenSource? workflowCancellation;
    private CancellationTokenSource? parameterRefreshCancellation;
    private bool updatingSelectedState;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the Setup workspace.</summary>
    /// <param name="activeVehicle">The shared active-vehicle context.</param>
    /// <param name="parameterRegistry">The shared vehicle parameter registry.</param>
    /// <param name="catalog">The setup workflow catalog.</param>
    /// <param name="completionStore">The local completion-evidence store.</param>
    /// <param name="workflowFactory">The lazy workflow view-model factory.</param>
    /// <param name="navigation">The Config navigation adapter.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public InitSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        ISetupWorkflowCatalog catalog,
        ISetupCompletionStore completionStore,
        ISetupWorkflowViewModelFactory workflowFactory,
        ISetupNavigationService navigation,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        ILogger<InitSetupViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.catalog = catalog;
        this.completionStore = completionStore;
        this.workflowFactory = workflowFactory;
        this.navigation = navigation;
        this.confirmation = confirmation;
        this.clock = clock;
        this.dispatcher = dispatcher;
        this.logger = logger;
        Refresh();
    }

    /// <summary>Gets the relevant workflows in dependency order.</summary>
    public ObservableCollection<SetupWorkflowItemViewModel> Workflows { get; } = [];

    /// <summary>Gets or sets the selected workflow.</summary>
    [ObservableProperty]
    public partial SetupWorkflowItemViewModel? SelectedWorkflow { get; set; }

    /// <summary>Gets the lazily created selected-workflow host.</summary>
    [ObservableProperty]
    public partial Tabs.SetupWorkflowDetailViewModel? SelectedWorkflowViewModel { get; private set; }

    /// <summary>Gets the specialized firmware workflow when Firmware is selected.</summary>
    [ObservableProperty]
    public partial Tabs.FirmwareSetupViewModel? SelectedFirmwareViewModel { get; private set; }

    /// <summary>Gets whether the specialized firmware workflow is selected.</summary>
    public bool IsFirmwareSelected => SelectedFirmwareViewModel is not null;

    /// <summary>Gets the specialized frame workflow when Frame is selected.</summary>
    [ObservableProperty]
    public partial Tabs.FrameSetupViewModel? SelectedFrameViewModel { get; private set; }

    /// <summary>Gets whether the specialized frame workflow is selected.</summary>
    public bool IsFrameSelected => SelectedFrameViewModel is not null;

    /// <summary>Gets the specialized accelerometer workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.AccelerometerSetupViewModel? SelectedAccelerometerViewModel { get; private set; }

    /// <summary>Gets whether the specialized accelerometer workflow is selected.</summary>
    public bool IsAccelerometerSelected => SelectedAccelerometerViewModel is not null;

    /// <summary>Gets the specialized compass workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.CompassSetupViewModel? SelectedCompassViewModel { get; private set; }

    /// <summary>Gets whether the specialized compass workflow is selected.</summary>
    public bool IsCompassSelected => SelectedCompassViewModel is not null;

    /// <summary>Gets the specialized radio workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.RadioSetupViewModel? SelectedRadioViewModel { get; private set; }

    /// <summary>Gets whether the specialized radio workflow is selected.</summary>
    public bool IsRadioSelected => SelectedRadioViewModel is not null;

    /// <summary>Gets the specialized flight-mode workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.FlightModesSetupViewModel? SelectedFlightModesViewModel { get; private set; }

    /// <summary>Gets whether the specialized flight-mode workflow is selected.</summary>
    public bool IsFlightModesSelected => SelectedFlightModesViewModel is not null;

    /// <summary>Gets the specialized battery workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.BatterySetupViewModel? SelectedBatteryViewModel { get; private set; }

    /// <summary>Gets whether the specialized battery workflow is selected.</summary>
    public bool IsBatterySelected => SelectedBatteryViewModel is not null;

    /// <summary>Gets the specialized ESC and motor-test workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.EscMotorSetupViewModel? SelectedEscViewModel { get; private set; }

    /// <summary>Gets whether the specialized ESC workflow is selected.</summary>
    public bool IsEscSelected => SelectedEscViewModel is not null;

    /// <summary>Gets the specialized servo output workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.ServoOutputSetupViewModel? SelectedServoOutputViewModel { get; private set; }

    /// <summary>Gets whether the specialized servo output workflow is selected.</summary>
    public bool IsServoOutputSelected => SelectedServoOutputViewModel is not null;

    /// <summary>Gets the specialized optional-hardware workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.OptionalHardwareSetupViewModel? SelectedOptionalHardwareViewModel { get; private set; }

    /// <summary>Gets whether the specialized optional-hardware workflow is selected.</summary>
    public bool IsOptionalHardwareSelected => SelectedOptionalHardwareViewModel is not null;

    /// <summary>Gets the specialized safety workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.SafetySetupViewModel? SelectedSafetyViewModel { get; private set; }

    /// <summary>Gets whether the specialized safety workflow is selected.</summary>
    public bool IsSafetySelected => SelectedSafetyViewModel is not null;

    /// <summary>Gets the specialized summary workflow when selected.</summary>
    [ObservableProperty]
    public partial Tabs.SetupSummaryViewModel? SelectedSummaryViewModel { get; private set; }

    /// <summary>Gets whether the specialized summary workflow is selected.</summary>
    public bool IsSummarySelected => SelectedSummaryViewModel is not null;

    /// <summary>Gets whether the selected workflow may be recorded through generic manual review.</summary>
    public bool CanRecordSelectedWorkflowManually =>
        SelectedWorkflow?.Descriptor.Key is not SetupWorkflowKey.Frame and not SetupWorkflowKey.Accelerometer and
            not SetupWorkflowKey.Compass and not SetupWorkflowKey.Radio;

    /// <summary>Gets the active vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading { get; private set; } = "No vehicle connected";

    /// <summary>Gets the setup summary report.</summary>
    [ObservableProperty]
    public partial string SummaryReport { get; private set; } = string.Empty;

    /// <summary>Gets the latest shared workflow error.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Activates vehicle and parameter change tracking for the visible page.</summary>
    public void Activate()
    {
        if (active || disposed)
        {
            return;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameterRegistry.Changed += OnParameterChanged;
        Refresh();
    }

    /// <summary>Deactivates the page and cancels work tied to its selected workflow.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameterRegistry.Changed -= OnParameterChanged;
        CancelParameterRefresh();
        CancelWorkflow();
    }

    /// <summary>Runs a future workflow operation against the current vehicle cancellation boundary.</summary>
    /// <param name="operation">The vehicle-targeted operation.</param>
    /// <returns>A task that completes after success, cancellation, or failure is reflected in the shell.</returns>
    public async Task RunOperationAsync(Func<VehicleId, CancellationToken, Task> operation)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || SelectedWorkflow is null)
        {
            Error = "Connect a vehicle before starting setup work.";
            return;
        }

        CancelWorkflow();
        workflowCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        var operationToken = workflowCancellation.Token;
        var selectedKey = SelectedWorkflow.Descriptor.Key;
        Override(selectedKey, SetupWorkflowState.InProgress, "In progress…");
        logger.LogInformation("Starting Setup workflow {Workflow} for {VehicleId}.", selectedKey, vehicleId);
        try
        {
            await operation(vehicleId, operationToken);
            Error = null;
            logger.LogInformation("Completed Setup workflow operation {Workflow} for {VehicleId}.", selectedKey, vehicleId);
            Refresh();
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Cancelled Setup workflow {Workflow} for {VehicleId}.", selectedKey, vehicleId);
            Refresh();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Setup workflow {Workflow} failed for {VehicleId}.", selectedKey, vehicleId);
            Error = exception.Message;
            Override(selectedKey, SetupWorkflowState.Failed, exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Deactivate();
        DisposeWorkflowViewModels();
    }

    partial void OnSelectedWorkflowChanged(SetupWorkflowItemViewModel? value)
    {
        if (!updatingSelectedState)
        {
            CancelWorkflow();
        }

        if (value is null)
        {
            SelectedWorkflowViewModel = null;
            SelectedFirmwareViewModel = null;
            SelectedFrameViewModel = null;
            SelectedAccelerometerViewModel = null;
            SelectedCompassViewModel = null;
            SelectedRadioViewModel = null;
            SelectedFlightModesViewModel = null;
            SelectedBatteryViewModel = null;
            SelectedEscViewModel = null;
            SelectedServoOutputViewModel = null;
            SelectedOptionalHardwareViewModel = null;
            SelectedSafetyViewModel = null;
            SelectedSummaryViewModel = null;
            OnPropertyChanged(nameof(CanRecordSelectedWorkflowManually));
            return;
        }

        SelectedWorkflowViewModel = workflowViewModels.GetValueOrDefault(value.Descriptor.Key);
        if (SelectedWorkflowViewModel is null)
        {
            SelectedWorkflowViewModel = workflowFactory.Create(value.Descriptor);
            workflowViewModels[value.Descriptor.Key] = SelectedWorkflowViewModel;
        }

        SelectedFirmwareViewModel = SelectedWorkflowViewModel as Tabs.FirmwareSetupViewModel;
        SelectedFirmwareViewModel?.UpdateVehicle(activeVehicle.State);
        SelectedFrameViewModel = SelectedWorkflowViewModel as Tabs.FrameSetupViewModel;
        if (SelectedFrameViewModel is not null)
        {
            _ = SelectedFrameViewModel.LoadAsync();
        }

        SelectedAccelerometerViewModel = SelectedWorkflowViewModel as Tabs.AccelerometerSetupViewModel;
        SelectedCompassViewModel = SelectedWorkflowViewModel as Tabs.CompassSetupViewModel;
        if (SelectedCompassViewModel is not null)
        {
            _ = SelectedCompassViewModel.LoadAsync();
        }

        SelectedRadioViewModel = SelectedWorkflowViewModel as Tabs.RadioSetupViewModel;
        SelectedFlightModesViewModel = SelectedWorkflowViewModel as Tabs.FlightModesSetupViewModel;
        SelectedBatteryViewModel = SelectedWorkflowViewModel as Tabs.BatterySetupViewModel;
        SelectedEscViewModel = SelectedWorkflowViewModel as Tabs.EscMotorSetupViewModel;
        SelectedServoOutputViewModel = SelectedWorkflowViewModel as Tabs.ServoOutputSetupViewModel;
        SelectedOptionalHardwareViewModel = SelectedWorkflowViewModel as Tabs.OptionalHardwareSetupViewModel;
        SelectedSafetyViewModel = SelectedWorkflowViewModel as Tabs.SafetySetupViewModel;
        SelectedSummaryViewModel = SelectedWorkflowViewModel as Tabs.SetupSummaryViewModel;

        OnPropertyChanged(nameof(CanRecordSelectedWorkflowManually));
    }

    partial void OnSelectedFirmwareViewModelChanged(Tabs.FirmwareSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsFirmwareSelected));
    }

    partial void OnSelectedFrameViewModelChanged(Tabs.FrameSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsFrameSelected));
    }

    partial void OnSelectedAccelerometerViewModelChanged(Tabs.AccelerometerSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsAccelerometerSelected));
    }

    partial void OnSelectedCompassViewModelChanged(Tabs.CompassSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsCompassSelected));
    }

    partial void OnSelectedRadioViewModelChanged(Tabs.RadioSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsRadioSelected));
    }

    partial void OnSelectedFlightModesViewModelChanged(Tabs.FlightModesSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsFlightModesSelected));
    }

    partial void OnSelectedBatteryViewModelChanged(Tabs.BatterySetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsBatterySelected));
    }

    partial void OnSelectedEscViewModelChanged(Tabs.EscMotorSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsEscSelected));
    }

    partial void OnSelectedServoOutputViewModelChanged(Tabs.ServoOutputSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsServoOutputSelected));
    }

    partial void OnSelectedOptionalHardwareViewModelChanged(Tabs.OptionalHardwareSetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsOptionalHardwareSelected));
    }

    partial void OnSelectedSafetyViewModelChanged(Tabs.SafetySetupViewModel? value)
    {
        OnPropertyChanged(nameof(IsSafetySelected));
    }

    partial void OnSelectedSummaryViewModelChanged(Tabs.SetupSummaryViewModel? value)
    {
        OnPropertyChanged(nameof(IsSummarySelected));
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshCore();
    }

    [RelayCommand]
    private async Task MarkCompleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedWorkflow is null || activeVehicle.State is not { } state || !activeVehicle.IsOnline)
        {
            Error = "Connect a vehicle and select a workflow first.";
            return;
        }

        if (SelectedWorkflow.Descriptor.Key is SetupWorkflowKey.Frame or SetupWorkflowKey.Accelerometer)
        {
            Error = "This workflow is recorded only after explicit vehicle protocol confirmation.";
            return;
        }

        var accepted = await confirmation.ConfirmAsync(
            "Record setup completion",
            $"Record {SelectedWorkflow.Title} as reviewed for {state.DisplayName}? It will be revalidated after firmware or parameter changes.",
            "Record complete",
            cancellationToken);
        if (!accepted)
        {
            return;
        }

        var parameters = parameterRegistry.GetAllParameters(state.VehicleId);
        completionStore.Save(catalog.CreateEvidence(SelectedWorkflow.Descriptor.Key, state, parameters, clock.UtcNow));
        logger.LogInformation("Recorded local completion for Setup workflow {Workflow} on {VehicleId}.", SelectedWorkflow.Descriptor.Key, state.VehicleId);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenConfigAsync()
    {
        if (SelectedWorkflow?.Descriptor.ConfigDestination is not { } destination)
        {
            return;
        }

        try
        {
            await navigation.OpenConfigAsync(destination);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to open Config destination {Destination}.", destination);
            Error = exception.Message;
        }
    }

    private void RefreshCore()
    {
        var selectedKey = SelectedWorkflow?.Descriptor.Key;
        var snapshot = activeVehicle.Current;
        var parameters = snapshot.VehicleId is { } id ? parameterRegistry.GetAllParameters(id) : new Dictionary<string, MavLink.Parameters.VehicleParameter>();
        var evaluations = catalog.Evaluate(snapshot, parameters, completionStore.GetAll()).Where(item => item.IsVisible).ToArray();
        Workflows.Clear();
        foreach (var evaluation in evaluations)
        {
            Workflows.Add(new SetupWorkflowItemViewModel(evaluation));
        }

        VehicleHeading = snapshot.IsOnline
            ? $"{snapshot.DisplayName} · {snapshot.State!.Identity.Firmware.Family}"
            : snapshot.VehicleId is null
                ? "No vehicle connected"
                : $"{snapshot.DisplayName} · disconnected";
        var completed = evaluations.Count(item => item.State == SetupWorkflowState.Completed);
        var warnings = evaluations.Count(item => item.State is SetupWorkflowState.Warning or SetupWorkflowState.Failed);
        SummaryReport = $"{completed} of {evaluations.Length} relevant workflows completed; {warnings} require attention.";
        SelectedWorkflow = Workflows.FirstOrDefault(item => item.Descriptor.Key == selectedKey) ?? Workflows.FirstOrDefault();
    }

    private void Override(SetupWorkflowKey key, SetupWorkflowState state, string status)
    {
        var index = Workflows.ToList().FindIndex(item => item.Descriptor.Key == key);
        if (index < 0)
        {
            return;
        }

        var current = Workflows[index];
        var replacement = new SetupWorkflowItemViewModel(current.Evaluation with { State = state, StatusText = status });
        Workflows[index] = replacement;
        updatingSelectedState = true;
        try
        {
            SelectedWorkflow = replacement;
        }
        finally
        {
            updatingSelectedState = false;
        }
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            CancelParameterRefresh();
            CancelWorkflow();
            DisposeWorkflowViewModels();
            Refresh();
        });
    }

    private void OnParameterChanged(object? sender, VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId)
        {
            CancellationToken token;
            lock (parameterRefreshSync)
            {
                parameterRefreshCancellation?.Cancel();
                parameterRefreshCancellation?.Dispose();
                parameterRefreshCancellation = new CancellationTokenSource();
                token = parameterRefreshCancellation.Token;
            }

            _ = RefreshAfterParameterChangesAsync(token);
        }
    }

    private async Task RefreshAfterParameterChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            if (active && !cancellationToken.IsCancellationRequested)
            {
                dispatcher.Dispatch(Refresh);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer parameter update or page deactivation replaced this refresh.
        }
    }

    private void CancelParameterRefresh()
    {
        lock (parameterRefreshSync)
        {
            parameterRefreshCancellation?.Cancel();
            parameterRefreshCancellation?.Dispose();
            parameterRefreshCancellation = null;
        }
    }

    private void CancelWorkflow()
    {
        SelectedWorkflowViewModel?.Cancel();
        workflowCancellation?.Cancel();
        workflowCancellation?.Dispose();
        workflowCancellation = null;
    }

    private void DisposeWorkflowViewModels()
    {
        foreach (var viewModel in workflowViewModels.Values)
        {
            viewModel.Dispose();
        }

        workflowViewModels.Clear();
        SelectedWorkflowViewModel = null;
        SelectedFirmwareViewModel = null;
        SelectedFrameViewModel = null;
        SelectedAccelerometerViewModel = null;
        SelectedCompassViewModel = null;
        SelectedRadioViewModel = null;
        SelectedFlightModesViewModel = null;
        SelectedBatteryViewModel = null;
        SelectedEscViewModel = null;
        SelectedServoOutputViewModel = null;
        SelectedOptionalHardwareViewModel = null;
        SelectedSafetyViewModel = null;
        SelectedSummaryViewModel = null;
    }
}
