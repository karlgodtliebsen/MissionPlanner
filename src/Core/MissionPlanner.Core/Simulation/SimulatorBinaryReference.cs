namespace MissionPlanner.Core.Simulation;

/// <summary>References a simulator binary selected by a persisted profile.</summary>
/// <param name="Version">Version or user-provided version label.</param>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="Source">Source identifier, such as external or verified cache.</param>
/// <param name="InstallationId">Stable discovered installation identity, when pinned.</param>
public sealed record SimulatorBinaryReference(
    string Version,
    string ExecutablePath,
    string Source,
    string? InstallationId = null);
