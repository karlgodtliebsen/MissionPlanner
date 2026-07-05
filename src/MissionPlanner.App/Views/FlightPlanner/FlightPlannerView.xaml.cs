using UraniumUI.Pages;

namespace MissionPlanner.App.Views.FlightPlanner;

public partial class FlightPlannerView : UraniumContentPage
{
    public FlightPlannerView(FlightPlannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}