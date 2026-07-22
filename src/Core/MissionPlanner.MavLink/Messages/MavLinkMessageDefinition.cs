namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Describes the wire-level properties of a MAVLink message.
/// </summary>
/// <param name="MessageId">The numeric MAVLink message identifier.</param>
/// <param name="Name">The MAVLink dialect name.</param>
/// <param name="CrcExtra">The CRC extra byte.</param>
/// <param name="MinimumPayloadLength">The payload length without extension fields.</param>
/// <param name="MaximumPayloadLength">The payload length including extension fields.</param>
/// <param name="Dialect">The dialect that declares the message.</param>
/// <param name="IsDeprecated">Whether the source dialect marks the message as deprecated.</param>
public sealed record MavLinkMessageDefinition(
    uint MessageId,
    string Name,
    byte CrcExtra,
    byte MinimumPayloadLength,
    byte MaximumPayloadLength,
    string Dialect,
    bool IsDeprecated);
