using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.Common;

/// <summary>Persistent, non-modal firmware update notice.</summary>
public partial class FirmwareUpdateNoticeView : UserControlViewBase<FirmwareUpdateNoticeViewModel>
{
    /// <summary>Initializes the notice markup.</summary>
    public FirmwareUpdateNoticeView()
    {
        InitializeComponent();
    }
}
