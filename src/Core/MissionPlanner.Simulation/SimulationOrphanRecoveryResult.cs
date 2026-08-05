namespace MissionPlanner.Simulation;

/// <summary>Provides one orphan recovery result.</summary>
/// <param name="OwnedProcess">Persisted owned-process identity.</param>
/// <param name="State">Recovery state.</param>
/// <param name="Message">Diagnostic detail.</param>
public sealed record SimulationOrphanRecoveryResult(SimulationOwnedProcess OwnedProcess, SimulationOrphanRecoveryState State, string Message);
