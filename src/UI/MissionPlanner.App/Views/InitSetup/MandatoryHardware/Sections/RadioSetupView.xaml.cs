using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays radio telemetry and calibration controls.</summary>
public partial class RadioSetupView : SetupSectionView
{
    /// <summary>Initializes a new instance of the <see cref="RadioSetupView"/> class.</summary>
    public RadioSetupView()
    {
        InitializeComponent();
        ConfigureViewModel<RadioSetupViewModel>();
    }
}
