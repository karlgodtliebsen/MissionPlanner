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
    protected override async Task OnActivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightPlannerMissionMapViewModel; //To share the Map it is brought along by this code
        DomainException.ThrowIfNull(map);
        await map.ActivateAsync();

        await MapView.ActivateAsync(map);
        await base.OnActivateAsync();
        MapLoadingIndicator.IsRunning = false;
        MapLoadingIndicator.IsVisible = false;
    }

    /// <inheritdoc />
    protected override Task OnDeactivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightPlannerMissionMapViewModel;
        DomainException.ThrowIfNull(map);
        map.Deactivate();
        return DeactivateCoreAsync();

        async Task DeactivateCoreAsync()
        {
            await MapView.DeactivateAsync();
            await base.OnDeactivateAsync();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightPlannerMissionMapViewModel;
        DomainException.ThrowIfNull(map);
        map.Dispose();
        MapView.Dispose();
        base.Dispose();
    }
}
