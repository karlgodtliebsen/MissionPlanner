using MissionPlanner.App.Helpers;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// Represents the view for flight planning.
/// </summary>
public partial class FlightPlannerView : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
    /// </summary>
    public FlightPlannerView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<FlightPlannerViewModel>();
        BindingContext = viewModel;
    }
}
