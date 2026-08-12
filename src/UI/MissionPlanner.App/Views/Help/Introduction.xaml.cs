using MissionPlanner.App.Helpers;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Help;

public partial class Introduction : UraniumContentPage
{
    public Introduction()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<IntroductionViewModel>();
        BindingContext = viewModel;
    }
}
