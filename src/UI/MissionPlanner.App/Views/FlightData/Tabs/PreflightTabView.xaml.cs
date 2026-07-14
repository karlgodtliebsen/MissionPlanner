using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class PreflightTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreflightTabView"/> class.
    /// </summary>
    public PreflightTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<PreflightTabViewModel>();
        BindingContext = viewModel;
    }
}
