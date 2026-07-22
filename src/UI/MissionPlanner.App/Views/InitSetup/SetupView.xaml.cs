using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>
/// Provides the public API for SetupView.
/// </summary>
public partial class SetupView : UraniumContentPage
{
    private readonly SetupViewModel viewModel;

    /// <summary>
    /// Provides the public API for SetupView.
    /// </summary>
    public SetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<SetupViewModel>();
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
