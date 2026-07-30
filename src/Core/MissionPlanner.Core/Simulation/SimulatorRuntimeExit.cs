namespace MissionPlanner.Core.Simulation;

/// <summary>Describes runtime termination.</summary>
/// <param name="ExitCode">Runtime exit code when available.</param>
/// <param name="WasExpected">Whether the runtime considered the exit expected.</param>
/// <param name="Message">Optional termination detail.</param>
public sealed record SimulatorRuntimeExit(int? ExitCode, bool WasExpected, string? Message);
