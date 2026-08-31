using UraniumUI.Pages;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// View for editing vehicle parameters in a text format. It allows users to input parameter values and updates the corresponding parameters in a provided list.
/// </summary>
public partial class ParametersEditorView : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParametersEditorView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model to bind to the view.</param>
    public ParametersEditorView(ParametersEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
