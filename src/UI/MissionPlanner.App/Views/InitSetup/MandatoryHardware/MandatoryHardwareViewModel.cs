using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware;

/// <summary>Presents the vehicle-aware initial-setup workflow shell and cross-cutting state.</summary>
public partial class MandatoryHardwareViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupWorkflowCatalog catalog;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupNavigationService navigation;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<MandatoryHardwareViewModel> logger;
    private readonly object parameterRefreshSync = new();
    private CancellationTokenSource? parameterRefreshCancellation;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the Setup workspace shell.</summary>
    /// <param name="activeVehicle">The shared active-vehicle context.</param>
    /// <param name="parameterRegistry">The shared vehicle parameter registry.</param>
    /// <param name="catalog">The setup workflow catalog.</param>
    /// <param name="completionStore">The local completion-evidence store.</param>
    /// <param name="navigation">The Config navigation adapter.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public MandatoryHardwareViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        ISetupWorkflowCatalog catalog,
        ISetupCompletionStore completionStore,
        ISetupNavigationService navigation,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        ILogger<MandatoryHardwareViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.catalog = catalog;
        this.completionStore = completionStore;
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

    /// <summary>Gets whether Firmware is selected.</summary>
    public bool IsFirmwareSelected => IsSelected(SetupWorkflowKey.Firmware);

    /// <summary>Gets whether Frame is selected.</summary>
    public bool IsFrameSelected => IsSelected(SetupWorkflowKey.Frame);

    /// <summary>Gets whether Accelerometer is selected.</summary>
    public bool IsAccelerometerSelected => IsSelected(SetupWorkflowKey.Accelerometer);

    /// <summary>Gets whether Compass is selected.</summary>
    public bool IsCompassSelected => IsSelected(SetupWorkflowKey.Compass);

    /// <summary>Gets whether Radio is selected.</summary>
    public bool IsRadioSelected => IsSelected(SetupWorkflowKey.Radio);

    /// <summary>Gets whether Flight Modes is selected.</summary>
    public bool IsFlightModesSelected => IsSelected(SetupWorkflowKey.FlightModes);

    /// <summary>Gets whether Battery is selected.</summary>
    public bool IsBatterySelected => IsSelected(SetupWorkflowKey.Battery);

    /// <summary>Gets whether ESC is selected.</summary>
    public bool IsEscSelected => IsSelected(SetupWorkflowKey.Esc);

    /// <summary>Gets whether Servo Output is selected.</summary>
    public bool IsServoOutputSelected => IsSelected(SetupWorkflowKey.ServoOutput);

    /// <summary>Gets whether Optional Hardware is selected.</summary>
    public bool IsOptionalHardwareSelected => IsSelected(SetupWorkflowKey.OptionalHardware);

    /// <summary>Gets whether Safety is selected.</summary>
    public bool IsSafetySelected => IsSelected(SetupWorkflowKey.Safety);

    /// <summary>Gets whether Summary is selected.</summary>
    public bool IsSummarySelected => IsSelected(SetupWorkflowKey.Summary);

    /// <summary>Gets whether the selected workflow links to a Config page.</summary>
    public bool HasConfigDestination => SelectedWorkflow?.Descriptor.ConfigDestination is not null;

    /// <summary>Gets whether the selected workflow may be recorded through generic manual review.</summary>
    public bool CanRecordSelectedWorkflowManually =>
        SelectedWorkflow?.Descriptor.Key is { } key &&
        key is not SetupWorkflowKey.Frame and not SetupWorkflowKey.Accelerometer and
            not SetupWorkflowKey.Compass and not SetupWorkflowKey.Radio;

    /// <summary>Gets the active vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading { get; private set; } = "No vehicle connected";

    /// <summary>Gets the setup summary report.</summary>
    [ObservableProperty]
    public partial string SummaryReport { get; private set; } = string.Empty;

    /// <summary>Gets the latest shell-level error.</summary>
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

    /// <summary>Deactivates the shell-level connection and workflow-evaluation tracking.</summary>
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
    }

    partial void OnSelectedWorkflowChanged(SetupWorkflowItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsFirmwareSelected));
        OnPropertyChanged(nameof(IsFrameSelected));
        OnPropertyChanged(nameof(IsAccelerometerSelected));
        OnPropertyChanged(nameof(IsCompassSelected));
        OnPropertyChanged(nameof(IsRadioSelected));
        OnPropertyChanged(nameof(IsFlightModesSelected));
        OnPropertyChanged(nameof(IsBatterySelected));
        OnPropertyChanged(nameof(IsEscSelected));
        OnPropertyChanged(nameof(IsServoOutputSelected));
        OnPropertyChanged(nameof(IsOptionalHardwareSelected));
        OnPropertyChanged(nameof(IsSafetySelected));
        OnPropertyChanged(nameof(IsSummarySelected));
        OnPropertyChanged(nameof(HasConfigDestination));
        OnPropertyChanged(nameof(CanRecordSelectedWorkflowManually));
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

        if (!CanRecordSelectedWorkflowManually)
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
        Error = null;
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
            Error = null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to open Config destination {Destination}.", destination);
            Error = exception.Message;
        }
    }

    private bool IsSelected(SetupWorkflowKey key) => SelectedWorkflow?.Descriptor.Key == key;

    private void RefreshCore()
    {
        var selectedKey = SelectedWorkflow?.Descriptor.Key;
        var snapshot = activeVehicle.Current;
        var parameters = snapshot.VehicleId is { } id
            ? parameterRegistry.GetAllParameters(id)
            : new Dictionary<string, MavLink.Parameters.VehicleParameter>();
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

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (!SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            if (!active)
            {
                return;
            }

            CancelParameterRefresh();
            Refresh();
        });
    }

    private void OnParameterChanged(object? sender, VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId != activeVehicle.VehicleId)
        {
            return;
        }

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
}
