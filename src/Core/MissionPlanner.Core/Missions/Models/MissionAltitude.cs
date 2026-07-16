namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents the altitude of a mission item.
/// </summary>
/// <param name="Meters">The altitude in meters.</param>
/// <param name="Reference">The reference for the altitude.</param>
public readonly record struct MissionAltitude(double Meters, MissionAltitudeReference Reference);
