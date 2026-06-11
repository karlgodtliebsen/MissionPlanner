namespace MissionPlanner.Views.MenuHelp;

public partial class MenuHelpView : ContentView
{
    public MenuHelpView(MenuHelpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
