namespace MissionPlanner.App.Views.Common;

/// <summary>
/// Represents the error view.
/// </summary>
public partial class ErrorView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorView"/> class with the specified view model.
    /// </summary>
    public ErrorView(ErrorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
