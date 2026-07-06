namespace MissionPlanner.App.Views.Common;

/// <summary>
/// Persistent status bar control
/// </summary>
public partial class StatusBarView : ContentView
{
    public StatusBarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="viewModel"></param>
    public StatusBarView(StatusBarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
