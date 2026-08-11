using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Defines one explicit promoted-state projection.</summary>
public sealed record TelemetryFieldDescriptor(
    string Key,
    string Label,
    TelemetryFieldCategory Category,
    Func<VehicleState, object?> Value,
    Func<VehicleState, DateTimeOffset?> ObservedAt,
    string UnitKind,
    string Format,
    TelemetryGaugeType GaugeType,
    double? Minimum = null,
    double? Maximum = null);
