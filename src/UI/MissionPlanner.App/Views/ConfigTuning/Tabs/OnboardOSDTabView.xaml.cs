using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for OnboardOSDTabView.
/// </summary>
public partial class OnboardOSDTabView : ContentPage
{
    private readonly OnboardOsdTabViewModel viewModel;

    /// <summary>
    /// Provides the public API for OnboardOSDTabView.
    /// </summary>
    public OnboardOSDTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<OnboardOsdTabViewModel>();
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
