using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService navigationService;
    private readonly IWindowProvider windowProvider;

    public MainShellViewModel(INavigationService navigationService, IWindowProvider windowProvider)
    {
        this.navigationService = navigationService;
        this.windowProvider = windowProvider;
        navigationService.CurrentPageChanged += page => Content = page;
        MenuItems = CreateMenuItems();
        SelectedMenuItem = MenuItems[0];
    }

    public ObservableCollection<NavigationMenuItemViewModel> MenuItems
    {
        get;
    }

    [ObservableProperty]
    public partial Page? Content
    {
        get; private set;
    }

    [ObservableProperty]
    public partial bool IsNavigationCollapsed
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsNavigationOpen
    {
        get; set;
    }

    //partial void OnIsNavigationOpenChanged(bool oldValue, bool newValue)
    //{
    //    IsNavigationCollapsed = !newValue;
    //}

    [ObservableProperty]
    public partial NavigationMenuItemViewModel? SelectedMenuItem
    {
        get; set;
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

    [RelayCommand]
    private void Exit()
    {
        windowProvider.ActiveWindow?.Close();
    }

    private async void NavigateToSelectionAsync(string route)
    {
        await navigationService.NavigateAsync(route);
    }

    private static ObservableCollection<NavigationMenuItemViewModel> CreateMenuItems()
    {
        return [
        new("Flight Data", MissionPlannerRoutes.FlightData, "/Resources/Images/light_flightdata_icon.png"),
        new("Flight Planner", MissionPlannerRoutes.FlightPlanner, "/Resources/Images/light_flightplan_icon.png"),
        new("Setup", icon: "/Resources/Images/light_initialsetup_icon.png", children:
        [
            new("Install Firmware", MissionPlannerRoutes.SetupInstallFirmware),
            new("Mandatory Hardware", MissionPlannerRoutes.SetupMandatoryHardware),
            new("Optional Hardware", MissionPlannerRoutes.SetupOptionalHardware),
            new("Advanced", MissionPlannerRoutes.SetupAdvanced)
        ]),
        new("Config", icon: "/Resources/Images/light_tuningconfig_icon.png", children:
        [
            new("Geo Fence", MissionPlannerRoutes.ConfigGeoFence),
            new("Basic Tuning", MissionPlannerRoutes.ConfigBasicTuning),
            new("Extended Tuning", MissionPlannerRoutes.ConfigExtendedTuning),
            new("Onboard OSD", MissionPlannerRoutes.ConfigOnboardOSD),
            new("MAV FTP", MissionPlannerRoutes.ConfigMavFtp),
            new("Full Parameters List", MissionPlannerRoutes.ConfigFullParameters),
            new("CubeLAN 8 Port Switch", MissionPlannerRoutes.ConfigCubeLan8PortSwitch)
        ]),
        new("Preferences", MissionPlannerRoutes.Preferences),
        new("Simulation", MissionPlannerRoutes.Simulation, "/Resources/Images/light_simulation_icon.png"),
        new("Tutorial", MissionPlannerRoutes.Introduction),
        new("Help", MissionPlannerRoutes.Help, "/Resources/Images/light_help_icon.png")
    ];
    }
}
