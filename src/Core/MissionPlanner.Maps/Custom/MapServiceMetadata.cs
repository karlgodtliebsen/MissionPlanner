namespace MissionPlanner.Maps.Custom;

/// <summary>Summarizes parsed WMS or WMTS capabilities.</summary>
/// <param name="ServiceTitle">Service title.</param>
/// <param name="LayerNames">Advertised layer identifiers.</param>
/// <param name="TileMatrixSets">Advertised WMTS matrix sets.</param>
public sealed record MapServiceMetadata(string? ServiceTitle, IReadOnlyList<string> LayerNames, IReadOnlyList<string> TileMatrixSets);
