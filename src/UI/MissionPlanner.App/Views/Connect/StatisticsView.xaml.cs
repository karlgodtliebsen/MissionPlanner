using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Connect;

public partial class StatisticsView : ContentView
{
    public StatisticsView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<StatisticsViewModel>();
        BindingContext = viewModel;
    }
}
