namespace MissionPlanner.App.Views.Exit;

/// <inheritdoc />
public partial class ExitContentView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExitView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model for the exit view.</param>
    public ExitContentView(ExitViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
