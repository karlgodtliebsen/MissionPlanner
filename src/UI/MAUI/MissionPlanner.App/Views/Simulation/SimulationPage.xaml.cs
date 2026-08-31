using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Simulation;

/// <summary>
/// Provides the public API for SimulationPage.
/// </summary>
public partial class SimulationPage : ExtendedContentPage<SimulationViewModel>
{
    /// <summary>
    /// Provides the public API for SimulationPage.
    /// </summary>
    public SimulationPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override async Task OnActivateAsync()
    {
        await base.OnActivateAsync();
        await LocationMapView.CenterOnMyLocationAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        LocationMapView.Dispose();
        base.Dispose();
    }
}
