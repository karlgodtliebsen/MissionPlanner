namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Displays local combined HEX selection with distinct DFU validation semantics.</summary>
public partial class STM32DfuCustomHexView : UserControlViewBase<STM32BootloaderViewModel>
{
    /// <summary>Initializes the custom HEX view.</summary>
    public STM32DfuCustomHexView()
    {
        InitializeComponent();
    }
}
