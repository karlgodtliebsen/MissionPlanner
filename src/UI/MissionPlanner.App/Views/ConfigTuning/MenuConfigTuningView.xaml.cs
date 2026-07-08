using MissionPlanner.App.Views.ConfigTuning.Tabs;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>
/// Interaction logic for MenuConfigTuningView.xaml
/// </summary>
public partial class MenuConfigTuningView : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MenuConfigTuningView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model for the view.</param>
    /// <param name="fplView"></param>
    public MenuConfigTuningView(MenuConfigTuningViewModel viewModel, FullParametersListTabView fplView)
    {
        InitializeComponent();
        BindingContext = viewModel;

        FplTabItem.Content = fplView;
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        TabView.SelectedTab = FplTabItem;
    }
}
