namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Records one ordered script-step result.</summary>
public sealed record VehicleScriptStepResult(int Index, string Action, bool Succeeded, string Message, DateTimeOffset CompletedAt);
