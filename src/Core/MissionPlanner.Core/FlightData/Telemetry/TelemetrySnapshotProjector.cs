using System.Globalization;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Formats telemetry while preserving its raw domain value.</summary>
public sealed class TelemetrySnapshotProjector : ITelemetrySnapshotProjector
{
    /// <inheritdoc />
    public TelemetryValueSnapshot Project(TelemetryFieldDescriptor descriptor, VehicleState state, UnitSystem units, DateTimeOffset now)
    {
        var raw = descriptor.Value(state);
        var observed = descriptor.ObservedAt(state);
        if (raw is null)
        {
            return new TelemetryValueSnapshot(descriptor, null, "Unavailable", string.Empty, TelemetryFreshness.Unavailable, observed);
        }

        var freshness = observed is null || now - observed > TimeSpan.FromSeconds(10) ? TelemetryFreshness.Stale : TelemetryFreshness.Fresh;
        var value = raw;
        var unit = Unit(descriptor.UnitKind, units);
        if (raw is IConvertible convertible && descriptor.UnitKind is "speed" or "distance")
        {
            var number = convertible.ToDouble(CultureInfo.InvariantCulture);
            value = units == UnitSystem.Imperial ? descriptor.UnitKind == "speed" ? number * 2.236936 : number * 3.28084 : number;
        }

        var display = value is IFormattable formattable ? formattable.ToString(descriptor.Format, CultureInfo.CurrentCulture) : value.ToString() ?? "Unavailable";
        return new TelemetryValueSnapshot(descriptor, raw, display, unit, freshness, observed);
    }

    private static string Unit(string kind, UnitSystem units)
    {
        return kind switch
        {
            "angle" => "°",
            "speed" => units == UnitSystem.Imperial ? "mph" : "m/s",
            "distance" => units == UnitSystem.Imperial ? "ft" : "m",
            "voltage" => "V",
            "current" => "A",
            "percent" => "%",
            var _ => string.Empty
        };
    }
}
