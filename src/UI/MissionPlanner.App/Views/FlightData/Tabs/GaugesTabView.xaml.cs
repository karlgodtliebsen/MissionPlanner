using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class GaugesTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GaugesTabView"/> class.
    /// </summary>
    public GaugesTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<GaugesTabViewModel>();
        BindingContext = viewModel;
    }
}
