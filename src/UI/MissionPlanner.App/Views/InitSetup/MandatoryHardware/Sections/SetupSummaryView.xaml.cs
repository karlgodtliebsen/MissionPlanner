using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays and exports the current setup evidence summary.</summary>
public partial class SetupSummaryView : SetupSectionView
{
    private readonly SetupSummaryViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="SetupSummaryView"/> class.</summary>
    public SetupSummaryView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<SetupSummaryViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}
