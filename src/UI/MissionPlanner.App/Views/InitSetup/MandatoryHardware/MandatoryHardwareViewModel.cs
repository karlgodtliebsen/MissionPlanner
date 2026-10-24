using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware;

/// <summary>Presents the vehicle-aware initial-setup workflow shell and cross-cutting state.</summary>
public partial class MandatoryHardwareViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly Diagnostics.LiveTelemetryInspectorViewModel? inspector;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupWorkflowCatalog catalog;
    private readonly INavigationService navigation;
    private readonly Lock parameterRefreshSync = new();
    private Timer? parameterRefreshTimer;
    private bool active;
    private bool disposed;

    /// <summary>
    /// Initializes the Setup workspace shell.
    /// </summary>
    /// <param name="activeVehicle">The shared active-vehicle context.</param>
    /// <param name="parameterRegistry">The shared vehicle parameter registry.</param>
    /// <param name="catalog">The setup workflow catalog.</param>
    /// <param name="navigation">The Config navigation adapter.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="inspector">Optional existing Inspector presentation context.</param>
    public MandatoryHardwareViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        ISetupWorkflowCatalog catalog,
        INavigationService navigation,
        ILogger<MandatoryHardwareViewModel> logger,
        Diagnostics.LiveTelemetryInspectorViewModel? inspector = null) : base(logger)
    {
        this.inspector = inspector;
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.catalog = catalog;
        this.navigation = navigation;

    }

    /// <summary>Gets whether traditional-helicopter parameters are reported by the current firmware.</summary>
    [ObservableProperty] public partial bool SupportsTraditionalHelicopter { get; private set; }

    /// <summary>Gets the active vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading { get; private set; } = "No vehicle connected";

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        Debug.Print("MandatoryHardwareViewModel ActivateAsync Enter");
        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameterRegistry.Changed += OnParameterChanged;
        RefreshCore();
        Debug.Print("MandatoryHardwareViewModel ActivateAsync Exit");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Debug.Print("MandatoryHardwareViewModel DeactivateAsync Enter");
        Deactivate();
        Debug.Print("MandatoryHardwareViewModel DeactivateAsync Exit");
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameterRegistry.Changed -= OnParameterChanged;
        CancelParameterRefresh();
    }


    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Deactivate();
    }

    private void RefreshCore()
    {
        var snapshot = activeVehicle.Current;
        SetMessages(null, null);
        Dispatcher.Dispatch(() =>
        {
            SupportsTraditionalHelicopter = snapshot.VehicleId is { } id &&
                (parameterRegistry.GetParameter(id, "H_SW_TYPE") is not null || parameterRegistry.GetParameter(id, "H_RSC_MODE") is not null);
            VehicleHeading = snapshot.IsOnline
                ? $"{snapshot.DisplayName} · {snapshot.State!.Identity.Firmware.Family}"
                : snapshot.VehicleId is null ? "No vehicle connected" : $"{snapshot.DisplayName} · disconnected";
        });
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (!SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            CancelParameterRefresh();
            RefreshCore();
        });
    }

    private void OnParameterChanged(VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId != activeVehicle.VehicleId)
        {
            return;
        }

        lock (parameterRefreshSync)
        {
            if (!active || disposed)
            {
                return;
            }

            parameterRefreshTimer ??= new System.Threading.Timer(
                static state => ((MandatoryHardwareViewModel)state!).DispatchParameterRefresh(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            parameterRefreshTimer.Change(TimeSpan.FromMilliseconds(150), Timeout.InfiniteTimeSpan);
        }
    }

    private void DispatchParameterRefresh()
    {
        lock (parameterRefreshSync)
        {
            if (!active || disposed)
            {
                return;
            }
        }

        Dispatcher.Dispatch(() =>
        {
            if (active && !disposed)
            {
                RefreshCore();
            }
        });
    }

    private void CancelParameterRefresh()
    {
        Debug.Print("MandatoryHardwareViewModel CancelParameterRefresh Enter");
        lock (parameterRefreshSync)
        {
            parameterRefreshTimer?.Dispose();
            parameterRefreshTimer = null;
        }
        Debug.Print("MandatoryHardwareViewModel CancelParameterRefresh Exit");
    }
}
