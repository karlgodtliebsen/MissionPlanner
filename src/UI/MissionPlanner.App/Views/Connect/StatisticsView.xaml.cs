using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.Connect;

/// <summary>
/// Provides the public API for StatisticsView.
/// </summary>
public partial class StatisticsView : ContentView
{
    /// <summary>
    /// Provides the public API for StatisticsView.
    /// </summary>
    public StatisticsView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<StatisticsViewModel>();
        BindingContext = viewModel;
    }
}
