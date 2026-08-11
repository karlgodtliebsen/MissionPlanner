namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Contains raw and formatted telemetry with explicit freshness.</summary>
public sealed record TelemetryValueSnapshot(
    TelemetryFieldDescriptor Descriptor,
    object? RawValue,
    string DisplayValue,
    string Unit,
    TelemetryFreshness Freshness,
    DateTimeOffset? ObservedAt);
