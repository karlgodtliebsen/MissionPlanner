using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays the safety and arming assessment.</summary>
public partial class SafetySetupView : TabViewLifecycleContent<SafetySetupViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="SafetySetupView"/> class.</summary>
    public SafetySetupView()
    {
        InitializeComponent();
        // ConfigureViewModel<SafetySetupViewModel>();
    }
}
