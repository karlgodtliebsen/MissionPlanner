using Mapsui.Layers;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Library.Browser.Maps;

/// <summary>Online-only browser policy; rejects archives before reading or staging them.</summary>
public sealed class BrowserOfflineMaps : IOfflineMapPackInstaller, IOfflineMapPackValidator, IMapsuiMbTilesSourceFactory
{
    public const string UnsupportedMessage = "The browser app currently supports online maps only. Offline MBTiles packs can be used in the Windows app.";

    public ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedMessage);
    }

    public ValueTask ValidateAsync(OfflineMapPackManifest manifest, string archivePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedMessage);
    }

    public ILayer Create(ResolvedMapSource source) => throw new NotSupportedException(UnsupportedMessage);
}
