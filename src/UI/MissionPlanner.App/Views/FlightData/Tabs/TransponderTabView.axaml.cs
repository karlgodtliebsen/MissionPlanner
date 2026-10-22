using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>The Flight Data tab with the ADS-B transponder.</summary>
public partial class TransponderTabItemView : TabItemViewBase<TransponderTabViewModel>
{
    /// <summary>Creates the tab.</summary>
    public TransponderTabItemView()
    {
        InitializeComponent();
    }
}
