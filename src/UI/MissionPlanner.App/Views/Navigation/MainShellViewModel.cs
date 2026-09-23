using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>
/// ViewModel for the main shell of the application, responsible for managing navigation and menu items.
/// </summary>
public partial class MainShellViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService navigationService;
    private readonly IWindowProvider windowProvider;
    private IDisposable? eventHubDisposable;


    /// <summary>
    /// Initializes a new instance of the <see cref="MainShellViewModel"/> class.
    /// </summary>
    /// <param name="navigationService"></param>
    /// <param name="windowProvider"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="inspector"></param>
    public MainShellViewModel(INavigationService navigationService, IWindowProvider windowProvider, IDomainEventHub domainEventHub, Diagnostics.LiveTelemetryInspectorViewModel inspector)
    {
        Inspector = inspector;
        this.navigationService = navigationService;
        this.windowProvider = windowProvider;
        navigationService.CurrentPageChanged += page =>
        {
            Content = page;
            Inspector.SuggestContext(page.GetType().Name);
        };
        MenuItems = CreateMenuItems();
        SelectedMenuItem = MenuItems[0];

        eventHubDisposable = domainEventHub.SubscribeDomainEventAsync<ShowTelemetryEvent>(async (evt, ct) => ToggleTelemetry());
    }


    /// <inheritdoc />
    public void Dispose()
    {
        eventHubDisposable?.Dispose();
        eventHubDisposable = null;
    }


    /// <summary>Session-owned live telemetry Inspector.</summary>
    public Diagnostics.LiveTelemetryInspectorViewModel Inspector
    {
        get;
    }

    public ObservableCollection<NavigationMenuItemViewModel> MenuItems
    {
        get;
    }

    [ObservableProperty]
    public partial Page? Content
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsNavigationCollapsed
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool IsNavigationOpen
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial NavigationMenuItemViewModel? SelectedMenuItem
    {
        get;
        set;
    }


    partial void OnSelectedMenuItemChanged(NavigationMenuItemViewModel? value)
    {
        if (value?.Route is not null)
        {
            NavigateToSelectionAsync(value.Route);
        }
    }

    public Task InitializeAsync()
    {
        return navigationService.NavigateAsync(MissionPlannerRoutes.FlightData);
    }

    private void ToggleTelemetry()
    {
        Inspector.Toggle();
    }

    [RelayCommand]
    private void Exit()
    {
        windowProvider.ActiveWindow?.Close();
    }

    private async void NavigateToSelectionAsync(string route)
    {
        await navigationService.NavigateAsync(route);
        IsNavigationOpen = false;
        await Task.Yield();
    }

    private static Bitmap LoadImage(string image)
    {
        return new Bitmap(AssetLoader.Open(new Uri(image)));
    }

    private static ObservableCollection<NavigationMenuItemViewModel> CreateMenuItems()
    {
        return
        [
            new("Flight Data", MissionPlannerRoutes.FlightData, LoadImage("avares://MissionPlanner.App/Resources/Images/light_flightdata_icon.png")),
            new("Flight Planner", MissionPlannerRoutes.FlightPlanner, LoadImage("avares://MissionPlanner.App/Resources/Images/light_flightplan_icon.png")),
            new("Setup", icon: LoadImage("avares://MissionPlanner.App/Resources/Images/light_initialsetup_icon.png"), children:
            [
                new("Install Firmware", MissionPlannerRoutes.SetupInstallFirmware),
                new("Mandatory Hardware", MissionPlannerRoutes.SetupMandatoryHardware),
                new("Optional Hardware", MissionPlannerRoutes.SetupOptionalHardware),
                new("Arming", MissionPlannerRoutes.SetupArming),
                new("Advanced", MissionPlannerRoutes.SetupAdvanced)
            ]),
            new("Config", icon: LoadImage("avares://MissionPlanner.App/Resources/Images/light_tuningconfig_icon.png"), children:
            [
                new("Geo Fence", MissionPlannerRoutes.ConfigGeoFence),
                new("Basic Tuning", MissionPlannerRoutes.ConfigBasicTuning),
                new("Extended Tuning", MissionPlannerRoutes.ConfigExtendedTuning),
                new("Onboard OSD", MissionPlannerRoutes.ConfigOnboardOSD),
                new("MAV FTP", MissionPlannerRoutes.ConfigMavFtp),
                new("Full Parameters List", MissionPlannerRoutes.ConfigFullParameters),
                new("CubeLAN 8 Port Switch", MissionPlannerRoutes.ConfigCubeLan8PortSwitch)
            ]),
            new("Logs", MissionPlannerRoutes.Logs),
            new("Preferences", MissionPlannerRoutes.Preferences),
            new("Simulation", MissionPlannerRoutes.Simulation, LoadImage("avares://MissionPlanner.App/Resources/Images/light_simulation_icon.png")),
            new("Tutorial", MissionPlannerRoutes.Introduction),
            new("Help", MissionPlannerRoutes.Help, LoadImage("avares://MissionPlanner.App/Resources/Images/light_help_icon.png"))
        ];
    }
}

