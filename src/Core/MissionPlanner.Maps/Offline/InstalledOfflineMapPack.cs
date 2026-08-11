namespace MissionPlanner.Maps.Offline;

/// <summary>Describes an installed offline map pack.</summary>
/// <param name="Manifest">Validated pack manifest.</param>
/// <param name="DirectoryPath">Version installation directory.</param>
/// <param name="ArchivePath">Read-only MBTiles archive path.</param>
public sealed record InstalledOfflineMapPack(OfflineMapPackManifest Manifest, string DirectoryPath, string ArchivePath);
