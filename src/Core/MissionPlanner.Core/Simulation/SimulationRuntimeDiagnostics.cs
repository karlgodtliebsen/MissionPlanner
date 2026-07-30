namespace MissionPlanner.Core.Simulation;

/// <summary>Contains process and launch diagnostics supplied by a simulator runtime adapter.</summary>
/// <param name="ExecutablePath">Exact executable path.</param>
/// <param name="Arguments">Tokenized, unredacted launch arguments redacted only during export.</param>
/// <param name="RuntimeVersion">Selected or probed runtime version.</param>
/// <param name="ProcessStartedAt">Operating-system process start timestamp.</param>
/// <param name="Heartbeat">Bounded readiness statistics.</param>
public sealed record SimulationRuntimeDiagnostics(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string RuntimeVersion,
    DateTimeOffset? ProcessStartedAt,
    SimulationHeartbeatStatistics Heartbeat);
