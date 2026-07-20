using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>
/// Provides the public API for InitSetupView.
/// </summary>
public partial class InitSetupView : UraniumContentPage
{
    /// <summary>
    /// Provides the public API for InitSetupView.
    /// </summary>
    public InitSetupView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<InitSetupViewModel>();

        BindingContext = viewModel;
    }
}
