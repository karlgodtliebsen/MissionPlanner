using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Simulation;

public partial class SimulationView : UraniumContentPage
{
    public SimulationView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<SimulationViewModel>();

        BindingContext = viewModel;
    }
}
