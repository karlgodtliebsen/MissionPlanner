using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class TelemetryLogsTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryLogsTabView"/> class.
    /// </summary>
    public TelemetryLogsTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<TelemetryLogsTabViewModel>();
        BindingContext = viewModel;
    }
}
