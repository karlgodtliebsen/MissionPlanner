using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Contains bounded heartbeat readiness statistics for one simulator runtime.</summary>
/// <param name="ExpectedSystemId">SystemId allocated before launch.</param>
/// <param name="ConnectedVehicleId">Verified vehicle identity observed from replay-independent live telemetry.</param>
/// <param name="ObservedAt">Wall-clock time at successful readiness.</param>
/// <param name="ReadinessDuration">Elapsed process time before the verified heartbeat.</param>
/// <param name="VerifiedHeartbeatCount">Minimum number of identity-verified heartbeats observed during readiness.</param>
public sealed record SimulationHeartbeatStatistics(
    byte ExpectedSystemId,
    VehicleId? ConnectedVehicleId,
    DateTimeOffset? ObservedAt,
    TimeSpan? ReadinessDuration,
    long VerifiedHeartbeatCount);
