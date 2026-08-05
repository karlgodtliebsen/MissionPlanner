namespace MissionPlanner.Simulation;

/// <summary>Describes the simulation start location.</summary>
/// <param name="LatitudeDegrees">Latitude in decimal degrees.</param>
/// <param name="LongitudeDegrees">Longitude in decimal degrees.</param>
/// <param name="AltitudeMeters">Altitude above mean sea level in meters.</param>
/// <param name="HeadingDegrees">Initial heading in degrees.</param>
public sealed record SimulationLocation(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMeters,
    double HeadingDegrees);
