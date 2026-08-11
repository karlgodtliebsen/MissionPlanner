namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>An allow-listed action and its bounded string arguments.</summary>
public sealed record VehicleScriptStep(string Action, IReadOnlyDictionary<string, string> Arguments, int TimeoutSeconds = 15);
