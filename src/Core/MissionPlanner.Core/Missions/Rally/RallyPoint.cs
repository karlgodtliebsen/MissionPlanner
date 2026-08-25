using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>A rally location and its altitude semantics.</summary>
public sealed record RallyPoint(RallyPointId Id, GeoPosition Position, MissionAltitude Altitude);