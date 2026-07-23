using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays compass inventory, configuration, and calibration controls.</summary>
public partial class CompassSetupView : SetupSectionView
{
    private readonly CompassSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="CompassSetupView"/> class.</summary>
    public CompassSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<CompassSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
