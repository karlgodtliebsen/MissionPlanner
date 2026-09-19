namespace MissionPlanner.Shared.Models.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleConnectionData.
/// </summary>
/// <param name="State">The State value.</param>
/// <param name="LastHeartbeatAt">The LastHeartbeatAt value.</param>
public sealed record VehicleConnectionData(VehicleConnectionState State, DateTimeOffset LastHeartbeatAt)
{
    /// <summary>Last checksum/signature-accepted packet, independently of heartbeat.</summary>
    public DateTimeOffset? LastPacketAt { get; init; }
    /// <summary>Reason for definitive termination, when known.</summary>
    public string? DisconnectReason { get; init; }
    /// <summary>Active high-visibility armed connection warning, cleared on recovery.</summary>
    public string? Warning { get; init; }
}
