using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>The Flight Data tab for controlling the camera and gimbal payload.</summary>
public partial class PayloadControlTabItemView : TabItemViewBase<PayloadControlTabViewModel>
{
    /// <summary>Creates the tab.</summary>
    public PayloadControlTabItemView()
    {
        InitializeComponent();
    }
}
