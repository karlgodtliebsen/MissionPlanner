using UraniumUI.Pages;

namespace MissionPlanner.App.Views.MenuHelp;

public partial class MenuHelpView : UraniumContentPage
{
    public MenuHelpView(MenuHelpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}