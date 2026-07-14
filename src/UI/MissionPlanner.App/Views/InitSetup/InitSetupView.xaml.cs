using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup;

public partial class InitSetupView : UraniumContentPage
{
    public InitSetupView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<InitSetupViewModel>();

        BindingContext = viewModel;
    }
}
