using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Coordinates the Flight Data page, its active tab, and active-vehicle status presentation.
/// </summary>
public partial class FlightDataViewModel : BaseViewModel
{
    private readonly ILogger<FlightDataViewModel> logger;

    /// <summary>The shared mission map editor (same instance as the FlightData map).</summary>
    public MissionMapViewModel Map
    {
        get; private set;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataViewModel"/> class.
    /// </summary>
    /// <param name="map">The shared mission map editor.</param>
    /// <param name="logger">The logger.</param>
    public FlightDataViewModel(FlightDataMissionMapViewModel map, ILogger<FlightDataViewModel> logger) : base(logger)
    {
        Map = map;
        this.logger = logger;
        logger.LogTrace("FlightDataViewModel initialized.");
    }

}
