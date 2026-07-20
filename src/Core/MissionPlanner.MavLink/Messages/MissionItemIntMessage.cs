using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Provides the public API for MissionItemIntMessage.
/// </summary>
public sealed record MissionItemIntMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, float Param1, float Param2, float Param3, float Param4, int X, int Y, float Z, ushort Sequence, ushort Command, byte TargetSystem, byte TargetComponent, byte Frame, byte Current, byte AutoContinue, byte MissionType, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionItemInt, EndPoint, ReceivedAt);
