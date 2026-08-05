namespace MissionPlanner.Simulation;

/// <summary>Defines one typed, side-effect-free telemetry condition.</summary>
/// <param name="Metric">Telemetry metric.</param>
/// <param name="Operator">Comparison operator.</param>
/// <param name="Expected">Literal or declared variable value.</param>
/// <param name="Tolerance">Optional non-negative tolerance for numeric equality.</param>
public sealed record SimulationTelemetryCondition(
    SimulationTelemetryMetric Metric,
    SimulationComparisonOperator Operator,
    SimulationScenarioValue Expected,
    double? Tolerance = null);
