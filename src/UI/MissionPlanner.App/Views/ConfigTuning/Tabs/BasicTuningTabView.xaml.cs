using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for BasicTuningTabView.
/// </summary>
public partial class BasicTuningTabView : ContentPage
{
    private readonly BasicTuningTabViewModel viewModel;

    /// <summary>
    /// Provides the public API for BasicTuningTabView.
    /// </summary>
    public BasicTuningTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<BasicTuningTabViewModel>();
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
