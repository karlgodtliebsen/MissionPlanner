using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

public partial class StatusTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StatusTabView"/> class.
    /// </summary>
    public StatusTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<StatusTabViewModel>();
        BindingContext = viewModel;
    }
}
