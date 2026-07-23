using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays ESC calibration guidance and motor-test controls.</summary>
public partial class EscMotorSetupView : SetupSectionView
{
    private readonly EscMotorSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="EscMotorSetupView"/> class.</summary>
    public EscMotorSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<EscMotorSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
