using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Builds read-only setup context while keeping stale measurements distinct from current observations.</summary>
public static class SetupTelemetrySummary
{
    /// <summary>Formats sensor IDs, orientation, range and quality; stale samples never appear as current readings.</summary>
    public static string Rangefinders(VehicleState? state, DateTimeOffset now)
    {
        if (state?.ConnectionState != VehicleConnectionState.Online)
        {
            return "No connected vehicle. Rangefinder readings unavailable.";
        }
        if (state.Range.Sensors.Count == 0)
        {
            return "No DISTANCE_SENSOR observations received. Sensor IDs do not necessarily match RNGFND parameter instance numbers.";
        }
        return string.Join(Environment.NewLine, state.Range.Sensors.Values.OrderBy(sensor => sensor.Id).Select(sensor =>
        {
            var reading = Fresh(sensor.ObservedAt, now) && sensor.DistanceMeters is { } distance && double.IsFinite(distance)
                ? $"{distance:F2} m" : "unavailable / stale";
            var quality = Fresh(sensor.ObservedAt, now) ? sensor.SignalQualityPercent?.ToString() ?? "unknown" : "stale";
            return $"Sensor {sensor.Id}, orientation {sensor.Orientation}: {reading}; limits {sensor.MinimumMeters:F2}–{sensor.MaximumMeters:F2} m; quality {quality}.";
        }));
    }

    /// <summary>Formats passive failsafe test context without inducing a failsafe, arming or changing mode.</summary>
    public static string Failsafe(VehicleState? state, DateTimeOffset now)
    {
        if (state?.ConnectionState != VehicleConnectionState.Online)
        {
            return "No connected vehicle. Live failsafe test context unavailable.";
        }
        var flight = Fresh(state.Flight.ObservedAt, now) ? $"{state.Flight.Mode}; {(state.IsArmed ? "ARMED" : "disarmed")}; system state {state.SystemStatus}" : "Flight status stale";
        var radio = Fresh(state.Radio.ObservedAt, now)
            ? string.Join(", ", state.Radio.ChannelsRaw.Select((pwm, index) => $"RC{index + 1}: {(pwm is > 0 and < ushort.MaxValue ? pwm.ToString() : "unknown")}")) : "RC input unavailable / stale";
        var primary = state.Power.Batteries.GetValueOrDefault((byte)0);
        var battery = primary is not null && Fresh(primary.ObservedAt, now)
            ? $"{primary.VoltageVolts?.ToString("F2") ?? "unknown"} V; {primary.RemainingPercent?.ToString() ?? "unknown"}%"
            : Fresh(state.Power.ObservedAt, now)
                ? $"{state.Power.BatteryVoltageVolts?.ToString("F2") ?? "unknown"} V; {state.Power.BatteryRemainingPercent?.ToString() ?? "unknown"}%"
                : "Battery unavailable / stale";
        var estimator = Fresh(state.Health.ObservedAt, now) ? state.Health.EkfHealthy?.ToString() ?? "unknown" : "unavailable / stale";
        return $"{flight}{Environment.NewLine}{radio}{Environment.NewLine}Battery: {battery}; EKF healthy: {estimator}.{Environment.NewLine}Observe the vehicle's mode and status messages during your controlled test. This page does not trigger a failsafe.";
    }

    private static bool Fresh(DateTimeOffset? observedAt, DateTimeOffset now)
    {
        return observedAt is { } time && time <= now && now - time <= TimeSpan.FromSeconds(3);
    }
}
