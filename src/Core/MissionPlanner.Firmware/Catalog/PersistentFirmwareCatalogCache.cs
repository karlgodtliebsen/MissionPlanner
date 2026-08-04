using System.Text.Json;
using MissionPlanner.Firmware.Configuration;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Layers process memory over an atomic persistent manifest cache.</summary>
public sealed class PersistentFirmwareCatalogCache(IFirmwareCachePathProvider paths) : IFirmwareCatalogCache
{
    private const int CurrentSchemaVersion = 1;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CachedFirmwareManifest? memory;
    private string CachePath => Path.Combine(paths.CacheRoot, "catalog", "manifest-cache.json");

    /// <inheritdoc />
    public async Task<CachedFirmwareManifest?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (memory is not null) return memory;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (memory is not null) return memory;
            if (!File.Exists(CachePath)) return null;
            try
            {
                await using var stream = new FileStream(CachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
                var stored = await JsonSerializer.DeserializeAsync<StoredManifest>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (stored is null || stored.SchemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(stored.ContentBase64)) return null;
                memory = new(Convert.FromBase64String(stored.ContentBase64), stored.RetrievedAt, stored.ETag, stored.LastModified, stored.SourceUri, stored.SchemaVersion);
                return memory;
            }
            catch (Exception exception) when (exception is IOException or JsonException or FormatException)
            {
                return null;
            }
        }
        finally { gate.Release(); }
    }

    /// <inheritdoc />
    public async Task SetAsync(CachedFirmwareManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var normalized = manifest with { Content = manifest.Content.ToArray(), SchemaVersion = CurrentSchemaVersion };
            var directory = Path.GetDirectoryName(CachePath)!;
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, $"manifest-{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    var stored = new StoredManifest(CurrentSchemaVersion, normalized.SourceUri, normalized.ETag, normalized.LastModified, normalized.RetrievedAt, Convert.ToBase64String(normalized.Content.Span));
                    await JsonSerializer.SerializeAsync(stream, stored, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                File.Move(temporary, CachePath, true);
                memory = normalized;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { gate.Release(); }
    }

    private sealed record StoredManifest(int SchemaVersion, Uri? SourceUri, string? ETag, DateTimeOffset? LastModified, DateTimeOffset RetrievedAt, string ContentBase64);
}
