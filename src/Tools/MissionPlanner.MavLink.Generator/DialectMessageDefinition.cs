namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Describes one message resolved from a MAVLink XML dialect.
/// </summary>
/// <param name="MessageId">The numeric message identifier.</param>
/// <param name="Name">The MAVLink message name.</param>
/// <param name="CrcExtra">The computed CRC extra byte.</param>
/// <param name="MinimumPayloadLength">The payload length excluding extension fields.</param>
/// <param name="MaximumPayloadLength">The payload length including extension fields.</param>
/// <param name="Dialect">The dialect that declares the message.</param>
/// <param name="IsDeprecated">Whether the message is deprecated.</param>
public sealed record DialectMessageDefinition(
    uint MessageId,
    string Name,
    byte CrcExtra,
    byte MinimumPayloadLength,
    byte MaximumPayloadLength,
    string Dialect,
    bool IsDeprecated);
