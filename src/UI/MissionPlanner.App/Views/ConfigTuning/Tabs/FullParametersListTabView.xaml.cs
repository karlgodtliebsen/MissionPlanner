namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Interaction logic for FullParametersListTabView.xaml
/// </summary>
public partial class FullParametersListTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model for the view.</param>
    public FullParametersListTabView(FullParametersListTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
