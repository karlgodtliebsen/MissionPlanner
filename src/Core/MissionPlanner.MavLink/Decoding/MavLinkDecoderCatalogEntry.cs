using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Associates one validated effective decoder with its registry definition and ownership category.
/// </summary>
/// <param name="Definition">The selected-dialect message definition.</param>
/// <param name="Decoder">The effective typed decoder.</param>
/// <param name="Kind">The decoder implementation and ownership category.</param>
public sealed record MavLinkDecoderCatalogEntry(
    MavLinkMessageDefinition Definition,
    IMavLinkMessageDecoder Decoder,
    MavLinkDecoderKind Kind);
