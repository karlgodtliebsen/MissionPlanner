using MissionPlanner.App.Helpers;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Help;

/// <summary>
/// Represents the view for help.
/// </summary>
public partial class HelpView : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HelpView"/> class.
    /// </summary>
    public HelpView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<HelpViewModel>();
        BindingContext = viewModel;
    }
}
