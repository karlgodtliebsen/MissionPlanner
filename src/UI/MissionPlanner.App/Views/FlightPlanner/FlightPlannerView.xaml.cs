using MissionPlanner.App.Navigation;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// Represents the view for flight planning.
/// </summary>
public partial class FlightPlannerView : ExtendedContentPage<FlightPlannerViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
    /// </summary>
    public FlightPlannerView()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DomainException.ThrowIfNull(ViewModel);
            var map = ViewModel.Map as FlightPlannerMissionMapViewModel; //To share the Map it is brought over
            DomainException.ThrowIfNull(map);
            await MapView.Activate(map);
            ItemListView.BindingContext = map;
        }
    }

    /// <inheritdoc />
    protected override async void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            MapView.Deactivate();
            ItemListView.BindingContext = null;
        }
    }
}
