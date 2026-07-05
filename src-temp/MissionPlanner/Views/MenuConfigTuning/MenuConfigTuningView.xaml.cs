namespace MissionPlanner.Views.MenuConfigTuning;

public partial class MenuConfigTuningView : ContentView
{
    public MenuConfigTuningView(MenuConfigTuningViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
