using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays flight-mode assignment and live switch-position controls.</summary>
public partial class FlightModesSetupView : TabViewLifecycleContent<FlightModesSetupViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FlightModesSetupView"/> class.</summary>
    public FlightModesSetupView()
    {
        InitializeComponent();
    }
}
