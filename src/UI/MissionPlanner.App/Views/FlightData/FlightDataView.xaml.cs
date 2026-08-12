using MissionPlanner.App.Navigation;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Represents the view for displaying flight data.
/// </summary>
public partial class FlightDataView : ExtendedContentPage<FlightDataViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataView"/> class.
    /// </summary>
    public FlightDataView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DomainException.ThrowIfNull(ViewModel);
            var map = ViewModel.Map as FlightDataMissionMapViewModel; //To share the Map it is brought over
            DomainException.ThrowIfNull(map);
            await MapView.Activate(map);
        }
    }

    /// <inheritdoc />
    protected override async void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            MapView.Deactivate();
        }
    }
}
