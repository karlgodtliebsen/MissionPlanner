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
    private bool synchronizingSelection;


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
        navigationService.CurrentPageChanged += OnCurrentPageChanged;
        MenuItems = CreateMenuItems();
        SelectedMenuItem = MenuItems[0];

        eventHubDisposable = domainEventHub.SubscribeDomainEventAsync<ShowTelemetryEvent>(async (evt, ct) => ToggleTelemetry());
    }


    /// <inheritdoc />
    public void Dispose()
    {
        navigationService.CurrentPageChanged -= OnCurrentPageChanged;
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
        if (!synchronizingSelection && value?.Route is not null)
        {
            NavigateToSelectionAsync(value.Route);
        }
    }

    public Task InitializeAsync()
    {
        return navigationService.NavigateAsync(MissionPlannerRoutes.FlightData);
    }

    private void OnCurrentPageChanged(Page page)
    {
        synchronizingSelection = true;
        try
        {
            var route = navigationService.CurrentRoute;
            if (route == MissionPlannerRoutes.Introduction) route = MissionPlannerRoutes.Help;
            SelectedMenuItem = FindMenuItem(MenuItems, route);
            Content = page;
            Inspector.SuggestContext(page.GetType().Name);
        }
        finally { synchronizingSelection = false; }
    }

    private static NavigationMenuItemViewModel? FindMenuItem(
        IEnumerable<NavigationMenuItemViewModel> items, string? route)
    {
        if (route is null) return null;
        foreach (var item in items)
        {
            var child = FindMenuItem(item.Children, route);
            if (child is not null) return child;
            if (item.Route is { } target && (route == target ||
                route.StartsWith(target + "#", StringComparison.Ordinal) ||
                route.StartsWith(target + "/", StringComparison.Ordinal))) return item;
        }
        return null;
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
            new("Configuration", MissionPlannerRoutes.Configuration, icon: LoadImage("avares://MissionPlanner.App/Resources/Images/light_tuningconfig_icon.png")),
            new("Logs", MissionPlannerRoutes.Logs),
            new("Preferences", MissionPlannerRoutes.Preferences),
            new("Simulation", MissionPlannerRoutes.Simulation, LoadImage("avares://MissionPlanner.App/Resources/Images/light_simulation_icon.png")),
            new("Help", MissionPlannerRoutes.Help, LoadImage("avares://MissionPlanner.App/Resources/Images/light_help_icon.png"))
        ];
    }
}
