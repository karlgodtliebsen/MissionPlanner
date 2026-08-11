using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Creates validated advanced mission items without UI dependencies.</summary>
public interface IAdvancedMissionItemService
{
    /// <summary>Adds a spline waypoint at a geographic location.</summary>
    MissionMapCommandAvailability AddSplineWaypoint(Mission mission, GeoPosition position, MissionAltitude altitude);
    /// <summary>Adds a jump to the first executable item.</summary>
    MissionMapCommandAvailability AddJumpToStart(Mission mission, int repeatCount);
    /// <summary>Adds a jump to an explicit zero-based MAVLink sequence.</summary>
    MissionMapCommandAvailability AddJump(Mission mission, ushort targetSequence, int repeatCount);
    /// <summary>Adds a modern ROI-location command.</summary>
    MissionMapCommandAvailability AddRoiLocation(Mission mission, GeoPosition position, MissionAltitude altitude);
}
