using MissionPlanner.MavLink.Decoding;

namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>
/// Provides the complete, startup-validated set of effective typed MAVLink decoders.
/// </summary>
public interface IMavLinkMessageDecoderCatalog
{
    /// <summary>Gets catalog entries in numeric message-ID order.</summary>
    IReadOnlyList<MavLinkDecoderCatalogEntry> Entries { get; }

    /// <summary>Gets exactly one effective decoder for every typed message schema.</summary>
    IReadOnlyList<IMavLinkMessageDecoder> Decoders { get; }
}
