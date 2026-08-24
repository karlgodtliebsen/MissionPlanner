using MissionPlanner.App.Navigation;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// Represents the view for flight planning.
/// </summary>
public partial class FlightPlannerPage : ExtendedContentPage<FlightPlannerViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerPage"/> class.
    /// </summary>
    public FlightPlannerPage()
    {
        InitializeComponent();
        MapLoadingIndicator.IsVisible = true;
        MapLoadingIndicator.IsRunning = true;
    }


    /// <inheritdoc />
    protected override async Task ActivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightPlannerMissionMapViewModel; //To share the Map it is brought along by this code
        DomainException.ThrowIfNull(map);
        await MapView.Activate(map);
        await base.ActivateAsync();
        MapLoadingIndicator.IsRunning = false;
        MapLoadingIndicator.IsVisible = false;
    }
}
