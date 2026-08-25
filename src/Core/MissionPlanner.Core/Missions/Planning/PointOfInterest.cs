using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Persistent local planning point, never a MAVLink mission item.</summary>
public sealed record PointOfInterest(PointOfInterestId Id, string Name, GeoPosition Position, double? AltitudeMeters,
    string? Description, string? Category, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);