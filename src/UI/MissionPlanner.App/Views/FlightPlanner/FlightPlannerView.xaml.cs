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
    //private FlightPlannerMissionMapView? mapView;
    //private readonly Layout? host = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
    /// </summary>
    public FlightPlannerView()
    {
        InitializeComponent();
        //host = FindByName("MapView") as Layout;
    }

    /// <inheritdoc/>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DomainException.ThrowIfNull(ViewModel);

            var factory = ServiceHelper.GetRequiredService<IDomainFactory>();
            var map = ViewModel.Map as FlightPlannerMissionMapViewModel;
            DomainException.ThrowIfNull(map);
            //mapView = factory.Create<FlightPlannerMissionMapView, FlightPlannerMissionMapViewModel>(map);
            //host?.Children.Add(mapView);


            ItemListView.BindingContext = map;
            // mapView.Initialize().FireAndForget();
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            //host?.Children.Clear();
            //mapView?.Dispose();
            //mapView = null;
            ItemListView.BindingContext = null;
        }
    }
}
