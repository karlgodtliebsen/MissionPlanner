namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class QuickTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuickTabView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model for the quick tab.</param>
    public QuickTabView(QuickTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
