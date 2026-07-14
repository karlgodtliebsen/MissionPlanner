using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class ServoRelayTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServoRelayTabView"/> class.
    /// </summary>
    public ServoRelayTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<ServoRelayTabViewModel>();
        BindingContext = viewModel;
    }
}
