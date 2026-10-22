using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>The Flight Data tab for operating servos and relays.</summary>
public partial class ServoRelayTabItemView : TabItemViewBase<ServoRelayTabViewModel>
{
    /// <summary>Creates the tab.</summary>
    public ServoRelayTabItemView()
    {
        InitializeComponent();
    }
}
