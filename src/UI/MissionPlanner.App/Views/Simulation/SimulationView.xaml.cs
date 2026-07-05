using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Simulation;

public partial class SimulationView : UraniumContentPage
{
    public SimulationView(SimulationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}