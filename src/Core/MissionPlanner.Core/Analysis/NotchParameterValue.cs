namespace MissionPlanner.Core.Analysis;

/// <summary>Read-only parameter value with firmware metadata.</summary>
/// <param name="Name">Exact parameter name.</param>
/// <param name="Value">Snapshot value.</param>
/// <param name="Description">Metadata-backed explanation, or explicit unavailability.</param>
public sealed record NotchParameterValue(string Name, double Value, string Description);