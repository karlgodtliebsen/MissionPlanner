using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays accelerometer and level calibration controls.</summary>
public partial class AccelerometerSetupView : SetupSectionView
{
    private readonly AccelerometerSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="AccelerometerSetupView"/> class.</summary>
    public AccelerometerSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<AccelerometerSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
