using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Map;

using UraniumUI.Pages;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// 
/// </summary>
public partial class FlightDataView : UraniumContentPage
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="viewModel"></param>
    /// <param name="hudView"></param>
    /// <param name="flightDataMapView"></param>
    public FlightDataView(
        FlightDataViewModel viewModel,
        HudView hudView,
        FlightDataMapView flightDataMapView)
    {
        InitializeComponent();
        BindingContext = viewModel;
        HudHost.Content = hudView;
        MapHost.Content = flightDataMapView;
    }
}