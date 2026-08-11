using MissionPlanner.App.Helpers;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>Hosts the mission map backed by the Flight Data mission editor.</summary>
public partial class FlightDataMissionMapView : MissionMapView
{
    /// <summary>Initializes a new instance of the <see cref="FlightDataMissionMapView"/> class.</summary>
    public FlightDataMissionMapView() : base(
        ServiceHelper.GetRequiredService<IPlannerSettingsService>(),
        ServiceHelper.GetRequiredService<IMapSourceResolver>(),
        ServiceHelper.GetRequiredService<IMapsuiBasemapFactory>(),
        ServiceHelper.GetRequiredService<IMapAttributionCoordinator>(),
        ServiceHelper.GetRequiredService<ITerrainElevationService>(),
        ServiceHelper.GetRequiredService<FlightDataMissionMapViewModel>())
    {
    }
}
