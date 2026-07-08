using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Map;
using MissionPlanner.App.Views.FlightData.Tabs;
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
    /// <param name="quickTabView"></param>
    public FlightDataView(FlightDataViewModel viewModel, HudView hudView, FlightDataMapView flightDataMapView, QuickTabView quickTabView)
    {
        InitializeComponent();
        BindingContext = viewModel;
        HudHost.Content = hudView;
        MapHost.Content = flightDataMapView;
        QuickTabView.Content = quickTabView;
    }
}
