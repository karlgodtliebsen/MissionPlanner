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
    }

    /// <inheritdoc />
    protected override async Task OnModelCreatedAsync(FlightDataViewModel viewModel)
    {
        DomainException.ThrowIfNull(viewModel);
        var map = viewModel.Map as FlightDataMissionMapViewModel; //To share the Map it is brought over
        DomainException.ThrowIfNull(map);
        MapLoadingIndicator.IsVisible = true;
        MapLoadingIndicator.IsRunning = true;
        try
        {
            // Allow the indicator to render before map initialization starts.
            await Task.Yield();
            await MapView.Activate(map);
        }
        finally
        {
            MapLoadingIndicator.IsRunning = false;
            MapLoadingIndicator.IsVisible = false;
        }
    }

    /// <inheritdoc />
    protected override void OnDestroyingModel(FlightDataViewModel viewModel)
    {
        MapLoadingIndicator.IsRunning = false;
        MapLoadingIndicator.IsVisible = false;
        MapView.Deactivate();
    }
}
