using UraniumUI.Pages;

namespace MissionPlanner.App.Views.MenuInitSetup;

public partial class MenuInitSetupView : UraniumContentPage
{
    public MenuInitSetupView(MenuInitSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}