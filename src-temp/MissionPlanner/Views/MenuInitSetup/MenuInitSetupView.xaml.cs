namespace MissionPlanner.Views.MenuInitSetup;

public partial class MenuInitSetupView : ContentView
{
    public MenuInitSetupView(MenuInitSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
