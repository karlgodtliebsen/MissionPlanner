using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays and exports the current setup evidence summary.</summary>
public partial class SetupSummaryView : SetupSectionView
{
    /// <summary>Initializes a new instance of the <see cref="SetupSummaryView"/> class.</summary>
    public SetupSummaryView()
    {
        InitializeComponent();
        ConfigureViewModel<SetupSummaryViewModel>();
    }
}
