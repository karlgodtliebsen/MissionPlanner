namespace MissionPlanner.Core.Simulation;

/// <summary>Contains sufficient process identity to recover only a MissionPlanner-owned orphan.</summary>
/// <param name="SessionId">Owning simulation session.</param>
/// <param name="OwnershipToken">Unique marker token.</param>
/// <param name="ProcessId">Operating-system process identifier.</param>
/// <param name="ExecutablePath">Normalized executable path.</param>
/// <param name="StartedAt">Operating-system process start time.</param>
public sealed record SimulationOwnedProcess(
    Guid SessionId,
    Guid OwnershipToken,
    int ProcessId,
    string ExecutablePath,
    DateTimeOffset StartedAt);
