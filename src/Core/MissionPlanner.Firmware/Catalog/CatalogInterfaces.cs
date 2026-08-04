using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Loads and filters the firmware release catalogue.</summary>
public interface IFirmwareCatalogService
{
    /// <summary>Gets a catalogue matching the request.</summary>
    Task<FirmwareCatalog> GetCatalogAsync(FirmwareCatalogRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Retrieves manifest bytes from a remote source.</summary>
public interface IFirmwareManifestClient
{
    /// <summary>Gets a manifest, optionally using cached HTTP validators.</summary>
    Task<FirmwareManifestResponse> GetAsync(Uri uri, CachedFirmwareManifest? cached, CancellationToken cancellationToken = default);
}

/// <summary>Parses source manifest bytes into normalized releases.</summary>
public interface IFirmwareManifestParser
{
    /// <summary>Parses a plain or gzip-compressed manifest.</summary>
    IReadOnlyList<FirmwareManifestEntry> Parse(ReadOnlyMemory<byte> content);

    /// <summary>Parses a manifest while reporting isolated entry failures.</summary>
    FirmwareManifestParseResult ParseWithDiagnostics(ReadOnlyMemory<byte> content);
}

/// <summary>Persists source manifest bytes independently of catalogue logic.</summary>
public interface IFirmwareCatalogCache
{
    /// <summary>Reads the last valid cached manifest.</summary>
    Task<CachedFirmwareManifest?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a validated manifest.</summary>
    Task SetAsync(CachedFirmwareManifest manifest, CancellationToken cancellationToken = default);
}
