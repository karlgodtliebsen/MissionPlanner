using System.Net;

namespace MissionPlanner.MavLink;

/// <summary>
/// Represents a MAVLink frame received from a MAVLink device.
/// </summary>
/// <param name="SystemId"></param>
/// <param name="ComponentId"></param>
/// <param name="IPEndPoint"></param>
/// <param name="MessageId"></param>
/// <param name="Sequence"></param>
/// <param name="Payload"></param>
/// <param name="RawBytes"></param>
/// <param name="ReceivedAt"></param>
public sealed record MavLinkFrame(byte SystemId, byte ComponentId, IPEndPoint IPEndPoint, uint MessageId, byte Sequence, ReadOnlyMemory<byte> Payload, ReadOnlyMemory<byte> RawBytes, DateTimeOffset ReceivedAt);