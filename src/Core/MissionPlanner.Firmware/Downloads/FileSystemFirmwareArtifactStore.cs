using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;

namespace MissionPlanner.Firmware.Downloads;

/// <summary>Stores artifact data and metadata as atomically published cache directories.</summary>
public sealed class FileSystemFirmwareArtifactStore(
    IFirmwareCachePathProvider paths,
    IOptions<FirmwareOptions> options,
    TimeProvider timeProvider) : IFirmwareArtifactStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyGates = new(StringComparer.Ordinal);
    private readonly string root = Path.Combine(paths.CacheRoot, "artifacts");

    /// <inheritdoc />
    public async Task<IFirmwareStoredArtifact?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = EntryDirectory(cacheKey);
        try
        {
            var metadataPath = Path.Combine(directory, "metadata.json");
            var dataPath = Path.Combine(directory, "artifact.bin");
            if (!File.Exists(metadataPath) || !File.Exists(dataPath)) return null;
            await using var metadataStream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var metadata = await JsonSerializer.DeserializeAsync<FirmwareArtifactMetadata>(metadataStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata is null || metadata.CacheKey != cacheKey || new FileInfo(dataPath).Length != metadata.Size) return null;
            return new Stored(dataPath, metadata);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    /// <inheritdoc />
    public Task<IFirmwareArtifactWriter> CreateTemporaryAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        var directory = Path.Combine(root, $".{cacheKey}.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(directory);
        IFirmwareArtifactWriter writer = new Writer(this, cacheKey, directory,
            new FileStream(Path.Combine(directory, "artifact.bin"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough));
        return Task.FromResult(writer);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FirmwareArtifactCacheEntry>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root)) return [];
        var entries = new List<FirmwareArtifactCacheEntry>();
        foreach (var directory in Directory.EnumerateDirectories(root).Where(path => !Path.GetFileName(path).StartsWith('.')))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryGetAsync(Path.GetFileName(directory), cancellationToken).ConfigureAwait(false) is { } stored)
                entries.Add(new(stored.Metadata, directory));
        }
        return entries;
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = EntryDirectory(cacheKey);
        if (!Directory.Exists(directory)) return Task.FromResult(false);
        Directory.Delete(directory, true);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root)) return;
        foreach (var partial in Directory.EnumerateDirectories(root, ".*.partial")) Directory.Delete(partial, true);
        var entries = (await EnumerateAsync(cancellationToken).ConfigureAwait(false)).OrderByDescending(entry => entry.Metadata.DownloadedAt).ToArray();
        long retained = 0;
        foreach (var entry in entries)
        {
            var expired = timeProvider.GetUtcNow() - entry.Metadata.DownloadedAt > options.Value.ArtifactCacheMaximumAge;
            if (expired || checked(retained + entry.Metadata.Size) > options.Value.ArtifactCacheQuotaBytes)
                await RemoveAsync(entry.Metadata.CacheKey, cancellationToken).ConfigureAwait(false);
            else retained += entry.Metadata.Size;
        }
    }

    private string EntryDirectory(string key) => Path.Combine(root, key);

    private async Task<IFirmwareStoredArtifact> PublishAsync(string key, string directory, FirmwareArtifactMetadata metadata, CancellationToken cancellationToken)
    {
        var keyGate = KeyGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var destination = EntryDirectory(key);
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            Directory.Move(directory, destination);
            return new Stored(Path.Combine(destination, "artifact.bin"), metadata);
        }
        finally { keyGate.Release(); }
    }

    private sealed class Writer(FileSystemFirmwareArtifactStore owner, string key, string directory, FileStream stream) : IFirmwareArtifactWriter
    {
        private bool committed;
        public Stream Stream => stream;
        public async Task<IFirmwareStoredArtifact> CommitAsync(FirmwareArtifactMetadata metadata, CancellationToken cancellationToken = default)
        {
            if (committed) throw new InvalidOperationException("Artifact writer is already committed.");
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Dispose();
            await File.WriteAllTextAsync(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(metadata), cancellationToken).ConfigureAwait(false);
            var stored = await owner.PublishAsync(key, directory, metadata, cancellationToken).ConfigureAwait(false);
            committed = true;
            return stored;
        }
        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            if (!committed && Directory.Exists(directory)) Directory.Delete(directory, true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Stored(string path, FirmwareArtifactMetadata metadata) : IFirmwareStoredArtifact
    {
        public FirmwareArtifactMetadata Metadata => metadata;
        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
        }
    }
}
