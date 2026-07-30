namespace MissionPlanner.Core.Simulation;

/// <summary>Uniquely identifies a runtime session owned by MissionPlanner.</summary>
/// <param name="RuntimeId">Adapter-provided exact runtime identity.</param>
/// <param name="Adapter">Runtime adapter name.</param>
/// <param name="ProcessId">Operating-system process identifier when applicable.</param>
public sealed record SimulatorRuntimeIdentity(string RuntimeId, string Adapter, int? ProcessId);
