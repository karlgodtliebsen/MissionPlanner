namespace MissionPlanner.Core.Vehicles;

using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink;

/// <summary>
/// Represents a status text message from a vehicle.
/// </summary>
/// <param name="VehicleId">The vehicle history that owns the message.</param>
/// <param name="SourceSystemId">The source MAVLink system ID.</param>
/// <param name="SourceComponentId">The source MAVLink component ID.</param>
/// <param name="Severity">The MAVLink severity.</param>
/// <param name="Text">The text of the message.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
/// <param name="ProtocolId">The MAVLink 2 chunk message ID, when present.</param>
/// <param name="IsAssembled">Whether multiple chunks were assembled.</param>
/// <param name="IsTruncated">Whether one or more chunks were missing or timed out.</param>
/// <param name="Identity">The store-assigned stable identity.</param>
public sealed record VehicleStatusText(
    VehicleId VehicleId,
    byte SourceSystemId,
    byte SourceComponentId,
    MavSeverity Severity,
    string Text,
    DateTimeOffset ReceivedAt,
    ushort? ProtocolId = null,
    bool IsAssembled = false,
    bool IsTruncated = false,
    long Identity = 0);
