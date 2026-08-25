using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Local antenna-tracker planning position; it does not represent hardware state.</summary>
public sealed record TrackerHomeSnapshot(GeoPosition Position, double? AltitudeMeters, DateTimeOffset UpdatedAt, string Source);