using MissionPlanner.App.Navigation;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Represents the view for displaying flight data.
/// </summary>
public partial class FlightDataPage : ExtendedContentPage<FlightDataViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataPage"/> class.
    /// </summary>
    public FlightDataPage()
    {
        InitializeComponent();
        MapLoadingIndicator.IsRunning = true;
        MapLoadingIndicator.IsVisible = true;
    }

    /// <inheritdoc />
    protected override async Task ActivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightDataMissionMapViewModel; //To share the Map it is brought along by this code
        DomainException.ThrowIfNull(map);
        await map.ActivateAsync();

        await MapView.ActivateAsync(map);
        await base.ActivateAsync();
        MapLoadingIndicator.IsRunning = false;
        MapLoadingIndicator.IsVisible = false;
    }

    /// <inheritdoc />
    protected override Task DeactivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightDataMissionMapViewModel;
        DomainException.ThrowIfNull(map);
        map.Deactivate();
        MapView.Deactivate();
        return base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        DomainException.ThrowIfNull(ViewModel);
        var map = ViewModel.Map as FlightDataMissionMapViewModel;
        DomainException.ThrowIfNull(map);
        map.Dispose();
        MapView.Dispose();
        base.Dispose();
    }
}
