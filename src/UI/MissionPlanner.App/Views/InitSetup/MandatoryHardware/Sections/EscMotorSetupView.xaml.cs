using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays ESC calibration guidance and motor-test controls.</summary>
public partial class EscMotorSetupView : TabViewLifecycleContent<EscMotorSetupViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="EscMotorSetupView"/> class.</summary>
    public EscMotorSetupView()
    {
        InitializeComponent();
    }
}
