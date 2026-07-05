using UraniumUI.Pages;

namespace MissionPlanner.App.Views.MenuConfigTuning;

public partial class MenuConfigTuningView : UraniumContentPage
{
    public MenuConfigTuningView(MenuConfigTuningViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}