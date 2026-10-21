using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions;

/// <summary>Reads the geographic position and altitude of mission items that carry them.</summary>
public static class MissionItemGeometry
{
    /// <summary>Extracts the map position of a mission item, if it has one.</summary>
    public static GeoPosition? PositionOf(MissionItem item)
    {
        return item switch
        {
            WaypointMissionItem x => x.Position,
            LandMissionItem x => x.Position,
            LoiterMissionItem x => x.Position,
            TakeoffMissionItem x => x.Position,
            SplineWaypointMissionItem x => x.Position,
            RoiLocationMissionItem x => x.Position,
            var _ => null
        };
    }

    /// <summary>Extracts the altitude of a mission item, if it has one.</summary>
    public static MissionAltitude? AltitudeOf(MissionItem item)
    {
        return item switch
        {
            WaypointMissionItem x => x.Altitude,
            LandMissionItem x => x.Altitude,
            LoiterMissionItem x => x.Altitude,
            TakeoffMissionItem x => x.Altitude,
            SplineWaypointMissionItem x => x.Altitude,
            RoiLocationMissionItem x => x.Altitude,
            var _ => null
        };
    }
}
