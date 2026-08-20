using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays hardware identification diagnostics for the active vehicle.</summary>
public partial class HwIdView : TabViewLifecycleContent<HwIdViewModel>
{
    /// <summary>Initializes a new HW ID view.</summary>
    public HwIdView()
    {
        InitializeComponent();
    }
}
