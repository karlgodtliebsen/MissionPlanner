namespace MissionPlanner.Core.Simulation;

/// <summary>Describes one profile or runtime validation problem.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Path">Profile field or host resource.</param>
/// <param name="Message">User-facing explanation.</param>
public sealed record SimulationValidationIssue(string Code, string Path, string Message);
