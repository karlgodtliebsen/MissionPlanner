using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Help;

public partial class HelpView : UraniumContentPage
{
    public HelpView(HelpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}