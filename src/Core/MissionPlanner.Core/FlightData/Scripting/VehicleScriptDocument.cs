namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>A versioned, declarative vehicle automation document.</summary>
public sealed record VehicleScriptDocument(int Version, string Name, IReadOnlyList<VehicleScriptStep> Steps);
