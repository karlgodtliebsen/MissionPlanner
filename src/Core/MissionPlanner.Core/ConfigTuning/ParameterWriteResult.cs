namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Projects the outcome of writing one parameter.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Outcome">The write outcome.</param>
/// <param name="Message">A user-facing explanation.</param>
public sealed record ParameterWriteResult(string Name, ParameterWriteOutcome Outcome, string Message);
