namespace MissionPlanner.Views.MenuFlightPlanner;

public partial class MenuFlightPlannerView : ContentView
{
    public MenuFlightPlannerView(MenuFlightPlannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
