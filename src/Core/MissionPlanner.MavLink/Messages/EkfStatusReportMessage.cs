using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents an ArduPilot MAVLink EKF_STATUS_REPORT message.
/// </summary>
public sealed record EkfStatusReportMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Flags,
    float VelocityVariance,
    float PositionHorizontalVariance,
    float PositionVerticalVariance,
    float CompassVariance,
    float TerrainAltitudeVariance,
    float? AirspeedVariance,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.EkfStatusReport, EndPoint, ReceivedAt);
