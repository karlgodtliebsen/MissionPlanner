using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class DataFlashLogsTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataFlashLogsTabView"/> class.
    /// </summary>
    public DataFlashLogsTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<DataFlashLogsTabViewModel>();
        BindingContext = viewModel;
    }
}
