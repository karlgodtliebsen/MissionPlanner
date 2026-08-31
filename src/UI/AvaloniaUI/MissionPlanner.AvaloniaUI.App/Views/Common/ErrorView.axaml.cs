using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Common;

/// <summary>
/// Represents the error view.
/// </summary>
public partial class ErrorView : UserControl
{
    public ErrorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorView"/> class with the specified view model.
    /// </summary>
    public ErrorView(ErrorViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
