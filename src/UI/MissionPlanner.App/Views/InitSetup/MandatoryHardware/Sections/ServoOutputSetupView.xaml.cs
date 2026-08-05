using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays servo output function assignments and live output descriptions.</summary>
public partial class ServoOutputSetupView : SetupSectionView
{
    /// <summary>Initializes a new instance of the <see cref="ServoOutputSetupView"/> class.</summary>
    public ServoOutputSetupView()
    {
        InitializeComponent();
        ConfigureViewModel<ServoOutputSetupViewModel>();
    }
}
