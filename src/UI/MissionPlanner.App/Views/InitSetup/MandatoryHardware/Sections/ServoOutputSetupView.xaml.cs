using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays servo output function assignments and live output descriptions.</summary>
public partial class ServoOutputSetupView : SetupSectionView
{
    private readonly ServoOutputSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="ServoOutputSetupView"/> class.</summary>
    public ServoOutputSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<ServoOutputSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
