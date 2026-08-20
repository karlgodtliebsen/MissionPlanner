using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays supported Failsafe parameters for the active vehicle.</summary>
public partial class FailSafeView : TabViewLifecycleContent<FailSafeViewModel>
{
    /// <summary>Initializes a new Failsafe view.</summary>
    public FailSafeView()
    {
        InitializeComponent();
    }
}
