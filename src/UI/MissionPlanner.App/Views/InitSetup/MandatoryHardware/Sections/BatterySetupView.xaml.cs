using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays battery monitor, failsafe, and calibration controls.</summary>
public partial class BatterySetupView : TabViewLifecycleContent<BatterySetupViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="BatterySetupView"/> class.</summary>
    public BatterySetupView()
    {
        InitializeComponent();
    }
}
