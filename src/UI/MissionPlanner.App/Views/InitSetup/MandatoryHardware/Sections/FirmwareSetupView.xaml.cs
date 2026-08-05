using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays connected firmware identity, discovery, download, and flashing controls.</summary>
public partial class FirmwareSetupView : TabViewLifecycleContent<FirmwareSetupViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FirmwareSetupView"/> class.</summary>
    public FirmwareSetupView()
    {
        InitializeComponent();
    }
}
