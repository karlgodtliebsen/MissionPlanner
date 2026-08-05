namespace MissionPlanner.Simulation;

/// <summary>Describes a relative launch offset without introducing autonomous formation control.</summary>
/// <param name="NorthMeters">North offset from the base home location.</param>
/// <param name="EastMeters">East offset from the base home location.</param>
/// <param name="AltitudeMeters">Altitude offset from the base home location.</param>
/// <param name="HeadingDegrees">Heading offset from the base home heading.</param>
public sealed record SimulationFormationOffset(
    double NorthMeters,
    double EastMeters,
    double AltitudeMeters = 0,
    double HeadingDegrees = 0);
