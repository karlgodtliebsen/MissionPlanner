namespace MissionPlanner;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
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