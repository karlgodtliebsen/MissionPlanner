namespace MissionPlanner.Simulation;

/// <summary>Stores a literal safe value or a reference to a declared variable.</summary>
/// <param name="Kind">Expected value type.</param>
/// <param name="BooleanValue">Boolean literal.</param>
/// <param name="NumberValue">Finite numeric literal.</param>
/// <param name="TextValue">Bounded text literal.</param>
/// <param name="Variable">Declared variable name, without expression syntax.</param>
public sealed record SimulationScenarioValue(SimulationScenarioValueKind Kind, bool? BooleanValue = null, double? NumberValue = null, string? TextValue = null, string? Variable = null);
