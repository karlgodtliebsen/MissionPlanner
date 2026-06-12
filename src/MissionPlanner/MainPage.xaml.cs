using MissionPlanner.Views;
using MissionPlanner.Views.Connect;
using MissionPlanner.Views.MenuConfigTuning;
using MissionPlanner.Views.MenuFlightPlanner;
using MissionPlanner.Views.MenuHelp;
using MissionPlanner.Views.MenuInitSetup;
using MissionPlanner.Views.MenuSimulation;

using FlightDataView = MissionPlanner.Views.FlightData.FlightDataView;

namespace MissionPlanner;

/// <summary>
/// Represents the main page of the application.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel mainViewModel;
    private readonly FlightDataView flightDataView;
    private readonly MenuFlightPlannerView flightPlannerView;
    private readonly MenuInitSetupView menuInitSetupView;
    private readonly MenuConfigTuningView menuConfigTuningView;
    private readonly MenuSimulationView menuSimulationView;
    private readonly MenuHelpView helpView;
    private readonly ConnectPopup connectPopup;
    private Button? selectedTab;
    private readonly ContentView mainContent;

    private static readonly Color SelectedTabColor = Color.FromArgb("#3e3e42");
    private static readonly Color SelectedTabBorderColor = Colors.DodgerBlue;
    private static readonly Color UnselectedTabColor = Color.FromArgb("#2d2d30");

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        SelectTab(MenuFlightData);
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class with the specified connect popup.
    /// </summary>
    /// <param name="connectPopup">The connect popup.</param>
    public MainPage(
        MainPageViewModel mainViewModel,
        FlightDataView flightDataView,
        MenuFlightPlannerView flightPlannerView,
        MenuInitSetupView menuInitSetupView,
        MenuConfigTuningView menuConfigTuningView,
        MenuSimulationView menuSimulationView,
        MenuHelpView helpView,
        ConnectPopup connectPopup) : this()
    {
        this.mainViewModel = mainViewModel;
        BindingContext = mainViewModel;
        this.flightDataView = flightDataView;
        this.flightPlannerView = flightPlannerView;
        this.menuInitSetupView = menuInitSetupView;
        this.menuConfigTuningView = menuConfigTuningView;
        this.menuSimulationView = menuSimulationView;
        this.helpView = helpView;
        this.connectPopup = connectPopup;

        ContentView p = this.FindByName<ContentView>("ConnectPopupOverlay");
        p.Content = connectPopup;
        connectPopup.CloseRequested += (_, _) => ConnectPopupOverlay.IsVisible = false;
        mainContent = this.FindByName<ContentView>("MainContent");
        mainContent.Content = flightDataView;
    }


    private void SelectTab(Button tab)
    {
        if (selectedTab == tab)
        {
            return;
        }

        if (selectedTab is not null)
        {
            selectedTab.BackgroundColor = UnselectedTabColor;
            selectedTab.BorderColor = Colors.Transparent;
        }

        tab.BackgroundColor = SelectedTabColor;
        tab.BorderColor = SelectedTabBorderColor;
        selectedTab = tab;
    }

    private void MenuFlightData_Clicked(object? sender, EventArgs e)
    {
        SelectTab(MenuFlightData);
        mainContent.Content = flightDataView;
    }

    private void MenuFlightPlanner_Clicked(object? sender, EventArgs e)
    {
        SelectTab(MenuFlightPlanner);
        mainContent.Content = flightPlannerView;
    }

    private void MenuInitSetup_Clicked(object? sender, EventArgs e)
    {
        SelectTab(MenuInitSetup);
        mainContent.Content = menuInitSetupView;
    }

    private void MenuConfigTuning_Clicked(object? sender, EventArgs e)
    {
        SelectTab(MenuConfigTuning);
        mainContent.Content = menuConfigTuningView;
    }

    private void MenuSimulation_Clicked(object? sender, EventArgs e)
    {
        SelectTab(MenuSimulation);
        mainContent.Content = menuSimulationView;
    }

    private void MenuHelp_Clicked(object? sender, EventArgs e)
    {
        SelectTab(MenuHelp);
        mainContent.Content = helpView;
    }

    private void MenuConnect_Clicked(object? sender, EventArgs e)
    {
        ConnectPopupOverlay.IsVisible = !ConnectPopupOverlay.IsVisible;
    }
}