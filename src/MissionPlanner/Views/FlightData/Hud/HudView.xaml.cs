namespace MissionPlanner.Views.FlightData.Hud;

public partial class HudView : ContentView
{
    public HudView(HudViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}