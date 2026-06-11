using MissionPlanner.Views.Connect;

namespace MissionPlanner;

/// <summary>
/// Represents the main page of the application.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly ConnectPopup _connectPopup;

    public MainPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class with the specified connect popup.
    /// </summary>
    /// <param name="connectPopup">The connect popup.</param>
    public MainPage(ConnectPopup connectPopup) : this()
    {
        ContentView p = this.FindByName<ContentView>("ConnectPopupOverlay");
        p.Content = connectPopup;
        _connectPopup = connectPopup;
    }

    private void MenuFlightData_Clicked(object? sender, EventArgs e)
    {
        // TODO: navigate to / show Flight Data view
    }

    private void MenuFlightPlanner_Clicked(object? sender, EventArgs e)
    {
        // TODO: navigate to / show Flight Planner view
    }

    private void MenuInitSetup_Clicked(object? sender, EventArgs e)
    {
        // TODO: navigate to / show Initial Setup view
    }

    private void MenuConfigTuning_Clicked(object? sender, EventArgs e)
    {
        // TODO: navigate to / show Config/Tuning view
    }

    private void MenuSimulation_Clicked(object? sender, EventArgs e)
    {
        // TODO: navigate to / show Simulation (SITL) view
    }

    private void MenuHelp_Clicked(object? sender, EventArgs e)
    {
        // TODO: navigate to / show Help view
    }

    private void MenuConnect_Clicked(object? sender, EventArgs e)
    {
        ConnectPopupOverlay.IsVisible = !ConnectPopupOverlay.IsVisible;
    }
}