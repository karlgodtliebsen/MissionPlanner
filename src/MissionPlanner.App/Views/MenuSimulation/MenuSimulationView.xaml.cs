using UraniumUI.Pages;

namespace MissionPlanner.App.Views.MenuSimulation;

public partial class MenuSimulationView : UraniumContentPage
{
    public MenuSimulationView(MenuSimulationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}