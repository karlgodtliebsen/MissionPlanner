namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Interaction logic for FullParametersListTabView.xaml
/// </summary>
public partial class FullParametersListTabView : ContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabView"/> class.
    /// </summary>
    public FullParametersListTabView(FullParametersListTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
