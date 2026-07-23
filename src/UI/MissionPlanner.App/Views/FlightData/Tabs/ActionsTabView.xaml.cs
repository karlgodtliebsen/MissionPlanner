using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Represents the actions tab view.
/// </summary>
public partial class ActionsTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionsTabView"/> class.
    /// </summary>
    public ActionsTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<ActionsTabViewModel>();
        BindingContext = viewModel;
    }
}
