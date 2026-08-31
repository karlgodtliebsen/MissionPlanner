using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// Optional Hardware entry point for the existing Onboard OSD workspace.
/// </summary>
public partial class OnboardOsdBridgeView : TabViewLifecycleContent<OnboardOsdBridgeViewModel>
{
    /// <summary>Initializes the view.</summary>
    public OnboardOsdBridgeView()
    {
        InitializeComponent();
    }
}
