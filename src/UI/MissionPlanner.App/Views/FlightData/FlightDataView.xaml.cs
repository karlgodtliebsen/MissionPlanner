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
    protected override async Task OnModelCreatedAsync(FlightDataViewModel viewModel)
    {
        DomainException.ThrowIfNull(viewModel);
        var map = viewModel.Map as FlightDataMissionMapViewModel; //To share the Map it is brought over
        DomainException.ThrowIfNull(map);
        await MapView.Activate(map);
    }

    /// <inheritdoc />
    protected override void OnDestroyingModel(FlightDataViewModel viewModel)
    {
        MapView.Deactivate();
    }
}
