namespace MissionPlanner.Simulation;

/// <summary>Describes a runtime start request without exposing process APIs.</summary>
/// <param name="SessionId">MissionPlanner-owned session identity.</param>
/// <param name="Profile">Validated launch profile.</param>
/// <param name="LogDirectory">Session-specific log directory.</param>
public sealed record SimulatorStartRequest(Guid SessionId, SimulatorProfile Profile, string LogDirectory);
