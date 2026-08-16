using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.Models;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Hosts discovered optional-hardware modules as independent editable groups.</summary>
public sealed partial class OptionalHardwareSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareService hardwareService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<OptionalHardwareSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the optional-hardware Setup workflow.</summary>
    /// <param name="workflowCatalog">The setup workflow catalog.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="hardwareService">The optional-hardware service.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public OptionalHardwareSetupViewModel(
        ISetupWorkflowCatalog workflowCatalog,
        IActiveVehicleContext activeVehicle,
        IOptionalHardwareService hardwareService,
        IDispatcher dispatcher,
        ILogger<OptionalHardwareSetupViewModel> logger
    )
        : base(workflowCatalog.Workflows.First(w => w.Key == SetupWorkflowKey.OptionalHardware))
    {
        this.activeVehicle = activeVehicle;
        this.hardwareService = hardwareService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
        LoadAsync().FireAndForget();
    }

    /// <summary>Gets the discovered optional-hardware modules.</summary>
    public ObservableRangeCollection<OptionalHardwareModuleViewModel> Modules { get; } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Load the connected vehicle's optional hardware.";

    /// <summary>Gets whether a confirmed change requires a reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; private set; }

    /// <summary>Gets whether any optional-hardware modules were discovered.</summary>
    public bool HasModules => Modules.Count > 0;

    /// <summary>Loads the available optional-hardware modules for the active vehicle.</summary>
    /// <returns>A task that completes after the modules are projected.</returns>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before loading optional hardware.";
            return;
        }

        var token = StartOperation();
        try
        {
            var modules = await hardwareService.GetModulesAsync(vehicleId, token);
            dispatcher.Dispatch(() => Show(modules));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading optional hardware failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        base.Dispose();
    }


    /// <summary>Writes one peripheral setting and reloads on success.</summary>
    /// <param name="parameterName">The parameter to write.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task ApplyAsync(string parameterName, double value)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before editing optional hardware.";
            return;
        }

        var token = StartOperation();
        try
        {
            var result = await hardwareService.SetValueAsync(vehicleId, parameterName, value, token);
            Status = result.Message;
            if (result.Success)
            {
                RebootRequired |= result.RequiresReboot;
                var modules = await hardwareService.GetModulesAsync(vehicleId, token);
                dispatcher.Dispatch(() => Show(modules, true));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying optional-hardware setting failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            return;
        }

        var token = StartOperation();
        try
        {
            await hardwareService.RefreshAsync(vehicleId, token);
            var modules = await hardwareService.GetModulesAsync(vehicleId, token);
            dispatcher.Dispatch(() =>
            {
                RebootRequired = false;
                Show(modules);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Refreshing optional hardware failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private CancellationToken StartOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        return operationCancellation.Token;
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            dispatcher.Dispatch(() => LoadAsync().FireAndForget());
        }
    }

    private void Show(IReadOnlyList<OptionalHardwareModuleView> modules, bool preserveStatus = false)
    {
        var models = modules.Select(x =>
            new OptionalHardwareModuleViewModel(x, (tuple) => ApplyAsync(tuple.Item1, tuple.Item2).FireAndForget()));
        Modules.Clear();
        Modules.AddRange(models);
        if (!preserveStatus)
        {
            Status = Modules.Count == 0
                ? "No optional hardware was detected for this vehicle."
                : "Configure detected optional hardware. Only applicable modules are shown.";
        }

        OnPropertyChanged(nameof(HasModules));
    }
}
