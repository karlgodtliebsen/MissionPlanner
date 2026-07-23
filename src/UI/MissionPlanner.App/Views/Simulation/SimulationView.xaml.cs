using MissionPlanner.App.Helpers;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Simulation;

/// <summary>
/// Provides the public API for SimulationView.
/// </summary>
public partial class SimulationView : UraniumContentPage
{
    private readonly SimulationViewModel viewModel;

    /// <summary>
    /// Provides the public API for SimulationView.
    /// </summary>
    public SimulationView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<SimulationViewModel>();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Activate();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        viewModel.Deactivate();
        base.OnDisappearing();
    }
}
