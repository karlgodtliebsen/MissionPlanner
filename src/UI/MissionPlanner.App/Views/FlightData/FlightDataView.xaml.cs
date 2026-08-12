using MissionPlanner.App.Helpers;
using MissionPlanner.App.Navigation;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Represents the view for displaying flight data.
/// </summary>
public partial class FlightDataView : ExtendedContentPage<FlightDataViewModel>
{
    private FlightDataMissionMapView? mapView;
    private readonly Layout? host = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataView"/> class.
    /// </summary>
    public FlightDataView()
    {
        InitializeComponent();
        host = FindByName("MapView") as Layout;
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            mapView = ServiceHelper.GetRequiredService<FlightDataMissionMapView>();
            host?.Children.Add(mapView);
            mapView.Initialize().FireAndForget();
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            host?.Children.Clear();
            mapView?.Dispose();
            mapView = null;
        }
    }
}
