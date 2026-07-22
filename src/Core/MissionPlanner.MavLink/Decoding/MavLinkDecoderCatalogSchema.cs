namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Describes the generated expectation for one effective typed MAVLink decoder.
/// </summary>
/// <param name="MessageId">The numeric message identifier.</param>
/// <param name="CrcExtra">The expected CRC extra byte.</param>
/// <param name="MinimumPayloadLength">The expected base-field payload length.</param>
/// <param name="MaximumPayloadLength">The expected extension-inclusive payload length.</param>
/// <param name="Kind">The decoder implementation and ownership category.</param>
public sealed record MavLinkDecoderCatalogSchema(
    uint MessageId,
    byte CrcExtra,
    byte MinimumPayloadLength,
    byte MaximumPayloadLength,
    MavLinkDecoderKind Kind);
