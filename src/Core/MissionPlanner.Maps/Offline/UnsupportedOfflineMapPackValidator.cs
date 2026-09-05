namespace MissionPlanner.Maps.Offline;

/// <summary>Rejects offline packs until a platform registers an archive validator.</summary>
public sealed class UnsupportedOfflineMapPackValidator : IOfflineMapPackValidator
{
    public ValueTask ValidateAsync(OfflineMapPackManifest manifest, string archivePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Offline MBTiles maps are not available on this platform. Select an online map source.");
    }
}
