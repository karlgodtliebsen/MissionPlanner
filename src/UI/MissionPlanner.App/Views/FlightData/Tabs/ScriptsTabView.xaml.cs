using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class ScriptsTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptsTabView"/> class.
    /// </summary>
    public ScriptsTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<ScriptsTabViewModel>();
        BindingContext = viewModel;
    }
}
