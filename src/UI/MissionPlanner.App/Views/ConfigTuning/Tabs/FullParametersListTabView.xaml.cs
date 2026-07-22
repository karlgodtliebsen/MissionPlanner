using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Interaction logic for FullParametersListTabView.xaml
/// </summary>
public partial class FullParametersListTabView : ContentPage
{
    private readonly FullParametersListTabViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabView"/> class.
    /// </summary>
    public FullParametersListTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FullParametersListTabViewModel>();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Activate();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        viewModel.Deactivate();
        base.OnDisappearing();
    }
}
