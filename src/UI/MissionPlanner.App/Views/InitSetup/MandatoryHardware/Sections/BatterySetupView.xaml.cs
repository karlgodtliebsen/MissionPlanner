using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays battery monitor, failsafe, and calibration controls.</summary>
public partial class BatterySetupView : SetupSectionView
{
    private readonly BatterySetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="BatterySetupView"/> class.</summary>
    public BatterySetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<BatterySetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
