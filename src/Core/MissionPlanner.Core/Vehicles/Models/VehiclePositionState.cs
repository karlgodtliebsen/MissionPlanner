namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehiclePositionState.
/// </summary>
/// <param name="LatitudeDegrees">The LatitudeDegrees value.</param>
/// <param name="LongitudeDegrees">The LongitudeDegrees value.</param>
/// <param name="AltitudeMslMeters">The AltitudeMslMeters value.</param>
/// <param name="RelativeAltitudeMeters">The RelativeAltitudeMeters value.</param>
/// <param name="LocalNorthMeters">The LocalNorthMeters value.</param>
/// <param name="LocalEastMeters">The LocalEastMeters value.</param>
/// <param name="LocalDownMeters">The LocalDownMeters value.</param>
/// <param name="HeadingDegrees">The HeadingDegrees value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
/// <param name="HomeLatitudeDegrees">The HomeLatitudeDegrees value.</param>
/// <param name="HomeLongitudeDegrees">The HomeLongitudeDegrees value.</param>
/// <param name="HomeAltitudeMslMeters">The HomeAltitudeMslMeters value.</param>
public sealed record VehiclePositionState(
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? LocalNorthMeters,
    double? LocalEastMeters,
    double? LocalDownMeters,
    double? HeadingDegrees,
    DateTimeOffset? ObservedAt,
    double? HomeLatitudeDegrees = null,
    double? HomeLongitudeDegrees = null,
    double? HomeAltitudeMslMeters = null)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehiclePositionState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
