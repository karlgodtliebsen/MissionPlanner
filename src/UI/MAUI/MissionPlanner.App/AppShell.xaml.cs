using MissionPlanner.App.AppViewModels;
using MissionPlanner.App.Helpers;

namespace MissionPlanner.App;

/// <summary>The main application Shell and guarded workspace navigation host.</summary>
public partial class AppShell : Shell
{
    /// <summary>Initializes the main application Shell.</summary>
    public AppShell()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<AppShellContentViewModel>();
        viewModel.CurrentShell = this;
        BindingContext = viewModel;
    }
}
