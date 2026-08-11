using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Provides the shared Gauges and Status descriptor catalog.</summary>
public sealed class TelemetryFieldCatalog : ITelemetryFieldCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<TelemetryFieldDescriptor> Fields { get; } =
    [
        D("roll", "Roll", TelemetryFieldCategory.Motion, s => Degrees(s.Motion.RollRadians), s => s.Motion.ObservedAt, "angle", "0.0", TelemetryGaugeType.Dial, -180, 180),
        D("pitch", "Pitch", TelemetryFieldCategory.Motion, s => Degrees(s.Motion.PitchRadians), s => s.Motion.ObservedAt, "angle", "0.0", TelemetryGaugeType.Dial, -90, 90),
        D("heading", "Heading", TelemetryFieldCategory.Position, s => s.Position.HeadingDegrees, s => s.Position.ObservedAt, "angle", "0", TelemetryGaugeType.Dial, 0, 360),
        D("ground-speed", "Ground speed", TelemetryFieldCategory.Motion, s => s.Motion.GroundSpeedMetersPerSecond, s => s.Motion.ObservedAt, "speed", "0.0", TelemetryGaugeType.Dial, 0, 60),
        D("air-speed", "Air speed", TelemetryFieldCategory.Motion, s => s.Motion.AirSpeedMetersPerSecond, s => s.Motion.ObservedAt, "speed", "0.0", TelemetryGaugeType.Dial, 0, 60),
        D("vertical-speed", "Climb rate", TelemetryFieldCategory.Motion, s => s.Motion.VerticalSpeedMetersPerSecond, s => s.Motion.ObservedAt, "speed", "0.0", TelemetryGaugeType.Bar, -15, 15),
        D("altitude-msl", "MSL altitude", TelemetryFieldCategory.Position, s => s.Position.AltitudeMslMeters, s => s.Position.ObservedAt, "distance", "0.0", TelemetryGaugeType.Numeric),
        D("altitude-relative", "Relative altitude", TelemetryFieldCategory.Position, s => s.Position.RelativeAltitudeMeters, s => s.Position.ObservedAt, "distance", "0.0", TelemetryGaugeType.Numeric),
        D("waypoint-distance", "Waypoint distance", TelemetryFieldCategory.Navigation, s => s.Navigation.WaypointDistanceMeters, s => s.Navigation.ObservedAt, "distance", "0", TelemetryGaugeType.Numeric),
        D("gps-fix", "GPS fix", TelemetryFieldCategory.Gps, s => s.Gps.FixType, s => s.Gps.ObservedAt, "text", "", TelemetryGaugeType.Numeric),
        D("gps-satellites", "GPS satellites", TelemetryFieldCategory.Gps, s => s.Gps.SatellitesVisible, s => s.Gps.ObservedAt, "count", "0", TelemetryGaugeType.Bar, 0, 30),
        D("battery-voltage", "Battery voltage", TelemetryFieldCategory.Power, s => s.Power.BatteryVoltageVolts, s => s.Power.ObservedAt, "voltage", "0.00", TelemetryGaugeType.Numeric),
        D("battery-current", "Battery current", TelemetryFieldCategory.Power, s => s.Power.BatteryCurrentAmps, s => s.Power.ObservedAt, "current", "0.0", TelemetryGaugeType.Numeric),
        D("battery-remaining", "Battery remaining", TelemetryFieldCategory.Power, s => s.Power.BatteryRemainingPercent, s => s.Power.ObservedAt, "percent", "0", TelemetryGaugeType.Bar, 0, 100),
        D("radio-rssi", "Radio RSSI", TelemetryFieldCategory.Radio, s => s.Radio.RssiPercent, s => s.Radio.ObservedAt, "percent", "0", TelemetryGaugeType.Bar, 0, 100),
        D("mode", "Flight mode", TelemetryFieldCategory.Flight, s => s.Flight.Mode, s => s.Connection.LastHeartbeatAt, "text", "", TelemetryGaugeType.Numeric),
        D("armed", "Armed", TelemetryFieldCategory.Flight, s => s.Flight.IsArmed, s => s.Connection.LastHeartbeatAt, "text", "", TelemetryGaugeType.Numeric),
        D("ekf", "EKF healthy", TelemetryFieldCategory.Health, s => s.Health.EkfHealthy, s => s.Health.ObservedAt, "text", "", TelemetryGaugeType.Numeric)
    ];

    private static TelemetryFieldDescriptor D(string key, string label, TelemetryFieldCategory category,
        Func<VehicleState, object?> value, Func<VehicleState, DateTimeOffset?> observed, string unit, string format,
        TelemetryGaugeType gauge, double? min = null, double? max = null)
    {
        return new TelemetryFieldDescriptor(key, label, category, value, observed, unit, format, gauge, min, max);
    }

    private static double? Degrees(double? radians)
    {
        return radians * 180 / Math.PI;
    }
}
