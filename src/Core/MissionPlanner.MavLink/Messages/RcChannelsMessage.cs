using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink RC_CHANNELS message.
/// </summary>
public sealed record RcChannelsMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint TimeBootMs,
    byte ChannelCount,
    IReadOnlyList<ushort> ChannelsRaw,
    byte Rssi,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.RcChannels, EndPoint, ReceivedAt);
