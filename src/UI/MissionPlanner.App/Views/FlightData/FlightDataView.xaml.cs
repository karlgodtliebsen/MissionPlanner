using MissionPlanner.App.Helpers;
using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Represents the view for displaying flight data.
/// </summary>
public partial class FlightDataView : ExtendedContentPage<FlightDataViewModel>
{
    private FlightDataMissionMapView? mapView;
    private readonly Layout? host = null;
    private long mapGeneration;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataView"/> class.
    /// </summary>
    public FlightDataView()
    {
        InitializeComponent();
        host = FindByName("MapView") as Layout;
    }

    /// <inheritdoc />
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            var generation = ++mapGeneration;
            await Dispatcher.DispatchAsync(() =>
            {
                if (generation != mapGeneration)
                {
                    return;
                }

                var replacement = ServiceHelper.GetRequiredService<FlightDataMissionMapView>();
                mapView = replacement;
                host?.Children.Add(replacement);
            });
        }
    }

    /// <inheritdoc />
    protected override async void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            ++mapGeneration;
            var departing = mapView;
            mapView = null;
            await Dispatcher.DispatchAsync(() =>
            {
                if (departing is not null)
                {
                    host?.Children.Remove(departing);
                    departing.Dispose();
                }
            });
        }
    }
}
