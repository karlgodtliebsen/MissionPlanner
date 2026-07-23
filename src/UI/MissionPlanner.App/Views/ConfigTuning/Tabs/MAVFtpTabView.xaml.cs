namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for MAVFtpTabView.
/// </summary>
public partial class MAVFtpTabView : ContentPageView<MavFtpTabViewModel>
{
    /// <summary>
    /// Provides the public API for MAVFtpTabView.
    /// </summary>
    public MAVFtpTabView() : base("//ConfigMavFtp")
    {
        InitializeComponent();
    }
}
