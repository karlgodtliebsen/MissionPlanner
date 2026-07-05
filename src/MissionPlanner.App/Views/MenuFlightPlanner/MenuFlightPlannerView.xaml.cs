using UraniumUI.Pages;

namespace MissionPlanner.App.Views.MenuFlightPlanner;

public partial class MenuFlightPlannerView : UraniumContentPage
{
    public MenuFlightPlannerView(MenuFlightPlannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}