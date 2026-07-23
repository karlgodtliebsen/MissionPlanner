using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays flight-mode assignment and live switch-position controls.</summary>
public partial class FlightModesSetupView : SetupSectionView
{
    private readonly FlightModesSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="FlightModesSetupView"/> class.</summary>
    public FlightModesSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FlightModesSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
