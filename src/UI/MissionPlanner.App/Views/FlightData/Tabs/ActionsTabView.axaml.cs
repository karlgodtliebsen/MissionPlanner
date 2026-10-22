using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>The Flight Data tab with vehicle actions such as arming and mode changes.</summary>
public partial class ActionsTabItemView : TabItemViewBase<ActionsTabViewModel>
{
    /// <summary>Creates the tab.</summary>
    public ActionsTabItemView()
    {
        InitializeComponent();
    }
}
