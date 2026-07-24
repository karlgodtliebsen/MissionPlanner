namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Provides a normalized axis-comparison value for one component.</summary>
/// <param name="Axis">The axis.</param>
/// <param name="Component">The component key.</param>
/// <param name="ParameterName">The vehicle parameter.</param>
/// <param name="PendingValue">The pending value.</param>
/// <param name="NormalizedMagnitude">The magnitude divided by the largest axis magnitude for the component.</param>
public sealed record AdvancedTuningComparisonValue(
    string Axis,
    string Component,
    string ParameterName,
    double PendingValue,
    double NormalizedMagnitude);
