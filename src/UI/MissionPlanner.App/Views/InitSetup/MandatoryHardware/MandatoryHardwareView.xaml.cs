using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware;

/// <summary>
/// Provides the public API for MandatoryHardwareView.
/// </summary>
public partial class MandatoryHardwareView : UraniumContentPage
{
    private readonly MandatoryHardwareViewModel viewModel;

    /// <summary>
    /// Provides the public API for MandatoryHardwareView.
    /// </summary>
    public MandatoryHardwareView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MandatoryHardwareViewModel>();
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
