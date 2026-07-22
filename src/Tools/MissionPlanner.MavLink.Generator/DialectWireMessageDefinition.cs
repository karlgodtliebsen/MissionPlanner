namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Describes one message and its protocol-order fields from a resolved MAVLink dialect.
/// </summary>
/// <param name="MessageId">The numeric message identifier.</param>
/// <param name="Name">The MAVLink message name.</param>
/// <param name="Description">The message description.</param>
/// <param name="CrcExtra">The computed CRC extra byte.</param>
/// <param name="MinimumPayloadLength">The base-field payload length.</param>
/// <param name="MaximumPayloadLength">The extension-inclusive payload length.</param>
/// <param name="Dialect">The declaring dialect.</param>
/// <param name="IsDeprecated">Whether the message is deprecated.</param>
/// <param name="Fields">The fields in XML protocol order.</param>
public sealed record DialectWireMessageDefinition(
    uint MessageId,
    string Name,
    string Description,
    byte CrcExtra,
    byte MinimumPayloadLength,
    byte MaximumPayloadLength,
    string Dialect,
    bool IsDeprecated,
    IReadOnlyList<DialectWireFieldDefinition> Fields);

/// <summary>
/// Describes one field in a MAVLink wire message.
/// </summary>
/// <param name="Name">The XML field name.</param>
/// <param name="DeclaredType">The XML scalar type.</param>
/// <param name="ArrayLength">The fixed array length, or one for a scalar.</param>
/// <param name="IsExtension">Whether the field is a MAVLink 2 extension.</param>
/// <param name="SourceIndex">The field's XML protocol-order index.</param>
/// <param name="WireOffset">The byte offset in the packed MAVLink payload.</param>
/// <param name="ElementSize">The wire size of one element.</param>
/// <param name="Description">The field description.</param>
/// <param name="EnumName">The referenced MAVLink enum name, if any.</param>
public sealed record DialectWireFieldDefinition(
    string Name,
    string DeclaredType,
    int ArrayLength,
    bool IsExtension,
    int SourceIndex,
    int WireOffset,
    int ElementSize,
    string Description,
    string? EnumName);
