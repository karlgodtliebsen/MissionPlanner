using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays radio telemetry and calibration controls.</summary>
public partial class RadioSetupView : SetupSectionView
{
    private readonly RadioSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="RadioSetupView"/> class.</summary>
    public RadioSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<RadioSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
