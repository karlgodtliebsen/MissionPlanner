using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the attitude of the drone, including roll, pitch, and yaw angles.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="Roll">The roll angle in radians.</param>
/// <param name="Pitch">The pitch angle in radians.</param>
/// <param name="Yaw">The yaw angle in radians.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record AttitudeMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    double Roll,
    double Pitch,
    double Yaw,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.Attitude,
        EndPoint,
        ReceivedAt);
