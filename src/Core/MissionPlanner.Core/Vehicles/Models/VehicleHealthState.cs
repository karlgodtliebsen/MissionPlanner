namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleHealthState.
/// </summary>
/// <param name="EkfFlags">The EkfFlags value.</param>
/// <param name="EkfHealthy">The EkfHealthy value.</param>
/// <param name="VelocityVariance">The VelocityVariance value.</param>
/// <param name="HorizontalPositionVariance">The HorizontalPositionVariance value.</param>
/// <param name="VerticalPositionVariance">The VerticalPositionVariance value.</param>
/// <param name="CompassVariance">The CompassVariance value.</param>
/// <param name="TerrainAltitudeVariance">The TerrainAltitudeVariance value.</param>
/// <param name="AirspeedVariance">The AirspeedVariance value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
/// <param name="SensorsPresent">The available onboard sensor bitmap.</param>
/// <param name="SensorsEnabled">The enabled onboard sensor bitmap.</param>
/// <param name="SensorsHealthy">The healthy onboard sensor bitmap.</param>
/// <param name="ControllerLoadPercent">The controller load percentage.</param>
/// <param name="CommunicationDropRatePercent">The communication drop rate percentage.</param>
/// <param name="CommunicationErrors">The communication error count.</param>
/// <param name="SystemObservedAt">The system-health reception timestamp.</param>
public sealed record VehicleHealthState(
    ushort? EkfFlags,
    bool? EkfHealthy,
    double? VelocityVariance,
    double? HorizontalPositionVariance,
    double? VerticalPositionVariance,
    double? CompassVariance,
    double? TerrainAltitudeVariance,
    double? AirspeedVariance,
    DateTimeOffset? ObservedAt,
    uint? SensorsPresent = null,
    uint? SensorsEnabled = null,
    uint? SensorsHealthy = null,
    double? ControllerLoadPercent = null,
    double? CommunicationDropRatePercent = null,
    ushort? CommunicationErrors = null,
    DateTimeOffset? SystemObservedAt = null)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleHealthState Empty { get; } = new(null, null, null, null, null, null, null, null, null);

    /// <summary>Returns whether system-health telemetry is older than <paramref name="maximumAge"/>.</summary>
    public bool IsSystemHealthStale(DateTimeOffset now, TimeSpan maximumAge) => SystemObservedAt is null || now - SystemObservedAt > maximumAge;
}
