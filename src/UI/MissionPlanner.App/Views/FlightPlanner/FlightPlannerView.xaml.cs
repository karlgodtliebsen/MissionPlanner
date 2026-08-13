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
    protected override async Task OnModelCreatedAsync(FlightPlannerViewModel viewModel)
    {
        DomainException.ThrowIfNull(viewModel);
        var map = viewModel.Map as FlightPlannerMissionMapViewModel; //To share the Map it is brought over
        DomainException.ThrowIfNull(map);
        await MapView.Activate(map);
        ItemListView.BindingContext = map;
    }


    /// <inheritdoc />
    protected override void OnDestroyingModel(FlightPlannerViewModel viewModel)
    {
        MapView.Deactivate();
        ItemListView.BindingContext = null;
    }
}
