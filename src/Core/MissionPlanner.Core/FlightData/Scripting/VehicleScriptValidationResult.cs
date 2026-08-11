namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Reports complete-document validation.</summary>
public sealed record VehicleScriptValidationResult(bool IsValid, IReadOnlyList<string> Errors);
