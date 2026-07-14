using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// Persistent status bar control
/// </summary>
public partial class StatusBarView : ContentView
{
    /// <summary>
    /// 
    /// </summary>
    public StatusBarView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<StatusBarViewModel>();
        BindingContext = viewModel;
    }
}
