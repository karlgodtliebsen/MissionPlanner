namespace MissionPlanner.App.Views.ConfigTuning.Sections;

/// <summary>Shows coupled P/I proposals before explicit local staging.</summary>
public partial class PidAdjustmentView : UserControlViewBase<PidAdjustmentViewModel>
{
    /// <summary>Initializes the optional tuning assistant.</summary>
    public PidAdjustmentView() => InitializeComponent();
}
