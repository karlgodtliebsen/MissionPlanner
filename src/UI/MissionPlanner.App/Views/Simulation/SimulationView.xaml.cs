using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Simulation;

/// <summary>
/// Provides the public API for SimulationView.
/// </summary>
public partial class SimulationView : UraniumContentPage
{
    /// <summary>
    /// Provides the public API for SimulationView.
    /// </summary>
    public SimulationView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<SimulationViewModel>();

        BindingContext = viewModel;
    }
}
