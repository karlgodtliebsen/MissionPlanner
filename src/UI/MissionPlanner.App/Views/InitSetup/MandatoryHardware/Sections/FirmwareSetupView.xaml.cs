using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays connected firmware identity, discovery, download, and flashing controls.</summary>
public partial class FirmwareSetupView : SetupSectionView
{
    private readonly FirmwareSetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="FirmwareSetupView"/> class.</summary>
    public FirmwareSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FirmwareSetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
