using MissionPlanner.App.Helpers;
using MissionPlanner.App.Navigation;
using MissionPlanner.Library;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// Represents the view for flight planning.
/// </summary>
public partial class FlightPlannerView : ExtendedContentPage<FlightPlannerViewModel>
{
    private FlightPlannerMissionMapView? mapView;
    private readonly Layout? host = null;
    private long mapGeneration;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
    /// </summary>
    public FlightPlannerView()
    {
        InitializeComponent();
        host = FindByName("MapView") as Layout;
    }

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DomainException.ThrowIfNull(ViewModel);
            var factory = ServiceHelper.GetRequiredService<IDomainFactory>();
            var map = ViewModel.Map as FlightPlannerMissionMapViewModel; //To share the Map it is brought over
            DomainException.ThrowIfNull(map);

            var generation = ++mapGeneration;
            await Dispatcher.DispatchAsync(() =>
            {
                if (generation != mapGeneration)
                {
                    return;
                }

                var replacement = factory.Create<FlightPlannerMissionMapView, FlightPlannerMissionMapViewModel>(map);
                mapView = replacement;
                host?.Children.Add(replacement);
                ItemListView.BindingContext = map;
            });
        }
    }

    /// <inheritdoc />
    protected override async void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            var generation = ++mapGeneration;
            var departing = mapView;
            mapView = null;
            await Dispatcher.DispatchAsync(() =>
            {
                if (departing is not null)
                {
                    host?.Children.Remove(departing);
                    departing.Dispose();
                }

                if (generation == mapGeneration)
                {
                    ItemListView.BindingContext = null;
                }
            });
        }
    }
}
