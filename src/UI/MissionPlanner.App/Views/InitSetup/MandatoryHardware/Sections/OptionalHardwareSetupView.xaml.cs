using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays discovered optional-hardware modules and their verified settings.</summary>
public partial class OptionalHardwareSetupView : SetupSectionView
{
    private readonly OptionalHardwareSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="OptionalHardwareSetupView"/> class.</summary>
    public OptionalHardwareSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<OptionalHardwareSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
