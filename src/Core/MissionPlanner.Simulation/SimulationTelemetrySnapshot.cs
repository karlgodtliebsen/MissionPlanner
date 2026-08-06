using MissionPlanner.Firmware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Captures auditable telemetry evidence at a step boundary.</summary>
/// <param name="ObservedAt">Snapshot time.</param>
/// <param name="ConnectionState">Vehicle connection state.</param>
/// <param name="Mode">Semantic mode.</param>
/// <param name="Armed">Whether the vehicle is armed.</param>
/// <param name="LandedState">Extended landed state.</param>
/// <param name="LatitudeDegrees">Latitude.</param>
/// <param name="LongitudeDegrees">Longitude.</param>
/// <param name="AltitudeMslMeters">Mean-sea-level altitude.</param>
/// <param name="RelativeAltitudeMeters">Relative altitude.</param>
/// <param name="GroundSpeedMetersPerSecond">Ground speed.</param>
/// <param name="BatteryRemainingPercent">Battery percentage.</param>
/// <param name="GpsFixType">Primary GPS fix type.</param>
public sealed record SimulationTelemetrySnapshot(
    DateTimeOffset ObservedAt,
    VehicleConnectionState ConnectionState,
    VehicleMode Mode,
    bool Armed,
    VehicleLandedState LandedState,
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? GroundSpeedMetersPerSecond,
    int? BatteryRemainingPercent,
    GpsFixType GpsFixType = GpsFixType.Unknown);
