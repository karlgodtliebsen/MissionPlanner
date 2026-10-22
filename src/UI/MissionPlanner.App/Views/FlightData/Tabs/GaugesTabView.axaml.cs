using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>The Flight Data tab with telemetry gauges.</summary>
public partial class GaugesTabItemView : TabItemViewBase<GaugesTabViewModel>
{
    /// <summary>Creates the tab.</summary>
    public GaugesTabItemView()
    {
        InitializeComponent();
    }
}
