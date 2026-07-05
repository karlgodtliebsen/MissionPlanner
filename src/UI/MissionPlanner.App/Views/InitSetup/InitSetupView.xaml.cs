using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup;

public partial class InitSetupView : UraniumContentPage
{
    public InitSetupView(InitSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}