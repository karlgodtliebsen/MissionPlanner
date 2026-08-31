using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for GeoFenceTabView.
/// </summary>
public partial class GeoFenceTabView : ExtendedContentPage<GeoFenceTabViewModel>
{
    /// <summary>
    /// Provides the public API for GeoFenceTabView.
    /// </summary>
    public GeoFenceTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override async Task OnActivateAsync()
    {
        await base.OnActivateAsync();
        await FenceMapView.CenterOnMyLocationAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        FenceMapView.Dispose();
        base.Dispose();
    }
}
