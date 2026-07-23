using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays the supported vehicle frame configuration workflow.</summary>
public partial class FrameSetupView : SetupSectionView
{
    private readonly FrameSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="FrameSetupView"/> class.</summary>
    public FrameSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FrameSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
