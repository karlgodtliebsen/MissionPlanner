namespace MissionPlanner.Core.Simulation;

/// <summary>Describes one live capability required by a scenario.</summary>
/// <param name="Name">Capability name.</param>
/// <param name="Available">Whether the exact target supports it.</param>
/// <param name="Reason">Availability evidence.</param>
public sealed record SimulationScenarioCapability(string Name, bool Available, string Reason);
