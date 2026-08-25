using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Owns session-local tracker home state.</summary>
public interface ITrackerHomeService
{ /// <summary>Raised when state changes.</summary>
    event Action? Changed; /// <summary>Gets current state.</summary>
    TrackerHomeSnapshot? Snapshot
    {
        get;
    } /// <summary>Updates local state only.</summary>
    void Set(GeoPosition position, double? altitudeMeters, DateTimeOffset updatedAt, string source);
}