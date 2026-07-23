using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for PlannerTabView.
/// </summary>
public partial class PlannerTabView : ContentPage
{
    private readonly PlannerTabViewModel viewModel;

    /// <summary>
    /// Provides the public API for PlannerTabView.
    /// </summary>
    public PlannerTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<PlannerTabViewModel>();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.ActivateAsync();
    }
}
