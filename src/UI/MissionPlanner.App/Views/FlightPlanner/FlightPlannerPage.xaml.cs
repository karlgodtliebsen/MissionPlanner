using Mapsui.UI.Maui;
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
    }

    /// <inheritdoc/>
    protected override async Task OnModelCreatedAsync(FlightPlannerViewModel viewModel)
    {
        DomainException.ThrowIfNull(viewModel);
        var map = viewModel.Map as FlightPlannerMissionMapViewModel; //To share the Map it is brought over
        DomainException.ThrowIfNull(map);
        MapLoadingIndicator.IsVisible = true;
        MapLoadingIndicator.IsRunning = true;
        try
        {
            // Allow the indicator to render before map initialization starts.
            await Task.Yield();
            await MapView.Activate(map);
            ItemListView.BindingContext = map;
        }
        finally
        {
            MapLoadingIndicator.IsRunning = false;
            MapLoadingIndicator.IsVisible = false;
        }
    }


    /// <inheritdoc />
    protected override void OnDestroyingModel(FlightPlannerViewModel viewModel)
    {
        MapLoadingIndicator.IsRunning = false;
        MapLoadingIndicator.IsVisible = false;
        MapView.Deactivate();
        ItemListView.BindingContext = null;
    }
}
