using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink AUTOPILOT_VERSION message.
/// </summary>
/// <param name="SystemId">The source MAVLink system ID.</param>
/// <param name="ComponentId">The source MAVLink component ID.</param>
/// <param name="EndPoint">The source transport endpoint.</param>
/// <param name="Capabilities">The reported MAVLink capability bits.</param>
/// <param name="FlightSoftwareVersion">The packed flight software version.</param>
/// <param name="MiddlewareSoftwareVersion">The packed middleware version.</param>
/// <param name="OperatingSystemSoftwareVersion">The packed operating-system version.</param>
/// <param name="BoardVersion">The board hardware version.</param>
/// <param name="FlightCustomVersion">The flight software custom version bytes.</param>
/// <param name="MiddlewareCustomVersion">The middleware custom version bytes.</param>
/// <param name="OperatingSystemCustomVersion">The operating-system custom version bytes.</param>
/// <param name="VendorId">The board vendor ID.</param>
/// <param name="ProductId">The board product ID.</param>
/// <param name="Uid">The legacy hardware UID.</param>
/// <param name="Uid2">The MAVLink 2 extended hardware UID.</param>
/// <param name="ReceivedAt">The reception timestamp.</param>
public sealed record AutopilotVersionMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ulong Capabilities,
    uint FlightSoftwareVersion,
    uint MiddlewareSoftwareVersion,
    uint OperatingSystemSoftwareVersion,
    uint BoardVersion,
    byte[] FlightCustomVersion,
    byte[] MiddlewareCustomVersion,
    byte[] OperatingSystemCustomVersion,
    ushort VendorId,
    ushort ProductId,
    ulong Uid,
    byte[] Uid2,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.AutopilotVersion, EndPoint, ReceivedAt);
