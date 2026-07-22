using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>
/// Provides the public API for InitSetupView.
/// </summary>
public partial class InitSetupView : UraniumContentPage
{
    private readonly InitSetupViewModel viewModel;

    /// <summary>
    /// Provides the public API for InitSetupView.
    /// </summary>
    public InitSetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<InitSetupViewModel>();
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
