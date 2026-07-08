using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Help;

public partial class HelpView : UraniumContentPage
{
    public HelpView(Exit.ExitViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
