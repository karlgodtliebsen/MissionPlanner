using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays flight-mode assignment and live switch-position controls.</summary>
public partial class FlightModesSetupView : SetupSectionView
{
    /// <summary>Initializes a new instance of the <see cref="FlightModesSetupView"/> class.</summary>
    public FlightModesSetupView()
    {
        InitializeComponent();
        ConfigureViewModel<FlightModesSetupViewModel>();
    }
}
