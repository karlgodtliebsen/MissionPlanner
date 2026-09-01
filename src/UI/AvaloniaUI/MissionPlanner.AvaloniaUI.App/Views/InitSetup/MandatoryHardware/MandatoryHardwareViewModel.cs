using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.Common;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware;

/// <summary>Presents the vehicle-aware initial-setup workflow shell and cross-cutting state.</summary>
public partial class MandatoryHardwareViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
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
    public MandatoryHardwareViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        ISetupWorkflowCatalog catalog,
        INavigationService navigation, ILogger<MandatoryHardwareViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.catalog = catalog;
        this.navigation = navigation;

    }

    /// <summary>
    /// Gets fixed index-aligned headers.
    /// </summary>
    public ObservableRangeCollection<TabItemViewModel> Tabs { get; } = [];

    /// <summary>Gets or sets the selected header.</summary>
    [ObservableProperty]
    public partial TabItemViewModel? SelectedTab
    {
        get; set;
    }

    /// <summary>
    /// Gets whether the selected workflow links to a Config page.
    /// </summary>
    public bool HasConfigDestination => SelectedTab?.Descriptor.ConfigDestination is not null;

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

    [RelayCommand]
    private void Refresh()
    {
        RefreshCore();
    }

    [RelayCommand]
    private async Task OpenConfigAsync()
    {
        if (SelectedTab?.Descriptor.ConfigDestination is not { } destination)
        {
            return;
        }

        try
        {
            var parts = destination.Split('|');
            if (parts.Length != 2 || !parts[0].Equals("Config", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported Config destination '{destination}'.");
            }

            var route = parts[1] switch
            {
                "Full Parameters List" => MissionPlannerRoutes.ConfigFullParameters,
                "Onboard OSD" => MissionPlannerRoutes.ConfigOnboardOSD,
                _ => throw new InvalidOperationException($"Unknown Config destination '{parts[1]}'.")
            };
            await navigation.NavigateAsync(route);
            SetMessages(null, null);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to open Config destination {Destination}.", destination);
            SetMessages(null, exception.Message);
        }
    }

    private bool IsSelected(SetupWorkflowKey key)
    {
        return SelectedTab?.Descriptor.Key == key.ToString();
    }

    private void RefreshCore()
    {
        var selectedKey = SelectedTab?.Descriptor.Key;
        var snapshot = activeVehicle.Current;
        //var parameters = snapshot.VehicleId is { } id
        //    ? parameterRegistry.GetAllParameters(id)
        //    : new Dictionary<string, MavLink.Parameters.VehicleParameter>();

        var tabs = new List<TabItemViewModel>(
            catalog.Workflows.Select(item =>
                new TabItemViewModel(new TabDescriptor(item.Key.ToString(), item.Title, item.Description, item.ConfigDestination)))
        );


        SetMessages(null, null);
        Dispatcher.Dispatch(() =>
        {
            VehicleHeading = snapshot.IsOnline
                ? $"{snapshot.DisplayName} · {snapshot.State!.Identity.Firmware.Family}"
                : snapshot.VehicleId is null ? "No vehicle connected" : $"{snapshot.DisplayName} · disconnected";
            Tabs.ReplaceRange(tabs);
            SelectedTab = Tabs.FirstOrDefault(item => item.Descriptor.Key == selectedKey) ?? Tabs.FirstOrDefault();
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
            Refresh();
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
                Refresh();
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
