namespace MissionPlanner.Views.MenuSimulation;

public partial class MenuSimulationView : ContentView
{
    public MenuSimulationView(MenuSimulationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
