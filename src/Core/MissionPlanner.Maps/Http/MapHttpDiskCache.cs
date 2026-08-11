using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MissionPlanner.Maps.Http;

/// <summary>Stores bounded HTTP response cache entries separately from offline packs.</summary>
public sealed class MapHttpDiskCache
{
    private readonly string root;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> keyGates = new(StringComparer.Ordinal);
    private readonly object evictionGate = new();
    private long budgetBytes;
    private long sizeBytes;

    /// <summary>Gets the configured global disk budget.</summary>
    public long BudgetBytes
    {
        get => Interlocked.Read(ref budgetBytes);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            Interlocked.Exchange(ref budgetBytes, value);
            EnforceBudget();
        }
    }

    /// <summary>Gets the current cache size in bytes.</summary>
    public long SizeBytes => Interlocked.Read(ref sizeBytes);

    /// <summary>Initializes a map HTTP disk cache.</summary>
    public MapHttpDiskCache(string root, long budgetBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetBytes);
        this.root = Path.GetFullPath(root);
        this.budgetBytes = budgetBytes;
        sizeBytes = Directory.Exists(this.root)
            ? new DirectoryInfo(this.root).EnumerateFiles("*.data", SearchOption.AllDirectories).Sum(file => file.Length)
            : 0;
    }

    /// <summary>Writes an entry and enforces the global disk budget.</summary>
    public async Task WriteAsync(MapCacheNamespace cacheNamespace, string requestKey, byte[] content, MapHttpCacheMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cacheNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
        ArgumentNullException.ThrowIfNull(content);
        var directory = GetNamespacePath(cacheNamespace);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestKey)));
        var dataPath = Path.Combine(directory, key + ".data");
        var metadataPath = Path.Combine(directory, key + ".json");
        var gate = keyGates.GetOrAdd(dataPath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            var previousLength = File.Exists(dataPath) ? new FileInfo(dataPath).Length : 0;
            var dataStaging = dataPath + $".tmp-{Guid.NewGuid():N}";
            var metadataStaging = metadataPath + $".tmp-{Guid.NewGuid():N}";
            try
            {
                await File.WriteAllBytesAsync(dataStaging, content, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(metadataStaging, JsonSerializer.Serialize(metadata), cancellationToken).ConfigureAwait(false);
                File.Move(dataStaging, dataPath, true);
                File.Move(metadataStaging, metadataPath, true);
                Interlocked.Add(ref sizeBytes, content.LongLength - previousLength);
            }
            finally
            {
                if (File.Exists(dataStaging))
                {
                    File.Delete(dataStaging);
                }

                if (File.Exists(metadataStaging))
                {
                    File.Delete(metadataStaging);
                }
            }
        }
        finally
        {
            gate.Release();
        }

        if (SizeBytes > BudgetBytes)
        {
            EnforceBudget();
        }
    }

    /// <summary>Reads an entry when present.</summary>
    public async Task<(byte[] Content, MapHttpCacheMetadata Metadata)?> ReadAsync(MapCacheNamespace cacheNamespace, string requestKey, CancellationToken cancellationToken = default)
    {
        var directory = GetNamespacePath(cacheNamespace);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestKey)));
        var dataPath = Path.Combine(directory, key + ".data");
        var metadataPath = Path.Combine(directory, key + ".json");
        if (!File.Exists(dataPath) || !File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            File.SetLastAccessTimeUtc(dataPath, DateTime.UtcNow);
            var content = await File.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
            var metadata = JsonSerializer.Deserialize<MapHttpCacheMetadata>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
            return metadata is null ? null : (content, metadata);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            RemoveEntry(dataPath, metadataPath);
            return null;
        }
    }

    /// <summary>Clears one source/product/style namespace.</summary>
    public void Clear(MapCacheNamespace cacheNamespace)
    {
        var path = GetNamespacePath(cacheNamespace);
        if (Directory.Exists(path))
        {
            Interlocked.Add(ref sizeBytes, -new DirectoryInfo(path).EnumerateFiles("*.data", SearchOption.AllDirectories).Sum(file => file.Length));
            Directory.Delete(path, true);
        }
    }

    /// <summary>Clears every cache namespace belonging to a source.</summary>
    public void ClearSource(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (!Directory.Exists(root))
        {
            return;
        }

        var directory = Path.Combine(root, EncodeSegment(sourceId));
        if (!Directory.Exists(directory))
        {
            return;
        }

        Interlocked.Add(ref sizeBytes, -new DirectoryInfo(directory).EnumerateFiles("*.data", SearchOption.AllDirectories).Sum(file => file.Length));
        Directory.Delete(directory, true);
    }

    /// <summary>Clears all HTTP cache entries.</summary>
    public void ClearAll()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        Interlocked.Exchange(ref sizeBytes, 0);
    }

    /// <summary>Creates cache metadata from standard HTTP headers.</summary>
    public static MapHttpCacheMetadata CreateMetadata(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var expiresAt = response.Content.Headers.Expires;
        if (expiresAt is null && response.Headers.CacheControl?.MaxAge is { } maximumAge)
        {
            expiresAt = DateTimeOffset.UtcNow + maximumAge;
        }

        return new MapHttpCacheMetadata(
            expiresAt,
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified);
    }

    /// <summary>Adds validators from cached metadata to a request.</summary>
    public static void ApplyValidators(HttpRequestMessage request, MapHttpCacheMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.EntityTag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(metadata.EntityTag));
        }

        if (metadata.LastModified is not null)
        {
            request.Headers.IfModifiedSince = metadata.LastModified;
        }
    }

    private string GetNamespacePath(MapCacheNamespace value) => Path.Combine(root, EncodeSegment(value.SourceId), EncodeSegment(value.ProductId), EncodeSegment(value.StyleId));

    private static string EncodeSegment(string value) => Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(value));

    private void EnforceBudget()
    {
        lock (evictionGate)
        {
            if (!Directory.Exists(root) || SizeBytes <= BudgetBytes)
            {
                return;
            }

            foreach (var file in new DirectoryInfo(root).EnumerateFiles("*.data", SearchOption.AllDirectories).OrderBy(file => file.LastAccessTimeUtc))
            {
                if (SizeBytes <= BudgetBytes)
                {
                    break;
                }

                RemoveEntry(file.FullName, Path.ChangeExtension(file.FullName, ".json"));
            }
        }
    }

    private void RemoveEntry(string dataPath, string metadataPath)
    {
        if (File.Exists(dataPath))
        {
            var length = new FileInfo(dataPath).Length;
            File.Delete(dataPath);
            Interlocked.Add(ref sizeBytes, -length);
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }
    }
}
