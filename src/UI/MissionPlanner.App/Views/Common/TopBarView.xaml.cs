namespace MissionPlanner.App.Views.Common;

/// <inheritdoc />
public partial class TopBarView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarView"/> class with the specified view model.
    /// </summary>
    /// <param name="viewModel"></param>
    public TopBarView(TopBarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
