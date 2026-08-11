using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.Maps.Http;

/// <summary>Configures bounded map HTTP requests.</summary>
/// <param name="UserAgent">Honest application User-Agent.</param>
/// <param name="Timeout">Per-request timeout.</param>
public sealed record MapHttpOptions(string UserAgent, TimeSpan Timeout)
{
    /// <summary>Gets safe default map HTTP options.</summary>
    public static MapHttpOptions Default { get; } = new("MissionPlanner/2.0 (+https://ardupilot.org/planner/)", TimeSpan.FromSeconds(20));
}

/// <summary>Creates centrally configured map HTTP clients.</summary>
public interface IMapHttpClientFactory
{
    /// <summary>Creates a client with a bounded timeout and honest User-Agent.</summary>
    HttpClient CreateClient();
}

/// <summary>Default map HTTP client factory.</summary>
public sealed class MapHttpClientFactory(HttpMessageHandler handler, MapHttpOptions options) : IMapHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient()
    {
        var client = new HttpClient(handler, disposeHandler: false) { Timeout = options.Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        return client;
    }
}

/// <summary>Identifies one isolated map cache namespace.</summary>
/// <param name="SourceId">Source identifier.</param>
/// <param name="ProductId">Product identifier.</param>
/// <param name="StyleId">Style identifier.</param>
public sealed record MapCacheNamespace(string SourceId, string ProductId, string StyleId)
{
    /// <summary>Gets a filesystem-safe stable key.</summary>
    public string Key => string.Join("_", new[] { SourceId, ProductId, StyleId }.Select(value => string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'))));
}

/// <summary>Metadata retained for a protocol-aware HTTP cache entry.</summary>
/// <param name="ExpiresAt">Optional expiry.</param>
/// <param name="EntityTag">Optional ETag.</param>
/// <param name="LastModified">Optional last-modified timestamp.</param>
public sealed record MapHttpCacheMetadata(DateTimeOffset? ExpiresAt, string? EntityTag, DateTimeOffset? LastModified);

/// <summary>Stores bounded HTTP response cache entries separately from offline packs.</summary>
public sealed class MapHttpDiskCache
{
    private readonly string root;
    private readonly long budgetBytes;

    /// <summary>Gets the configured global disk budget.</summary>
    public long BudgetBytes => budgetBytes;

    /// <summary>Gets the current cache size in bytes.</summary>
    public long SizeBytes => Directory.Exists(root)
        ? new DirectoryInfo(root).EnumerateFiles("*.data", SearchOption.AllDirectories).Sum(file => file.Length)
        : 0;

    /// <summary>Initializes a map HTTP disk cache.</summary>
    public MapHttpDiskCache(string root, long budgetBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetBytes);
        this.root = Path.GetFullPath(root);
        this.budgetBytes = budgetBytes;
    }

    /// <summary>Writes an entry and enforces the global disk budget.</summary>
    public async Task WriteAsync(MapCacheNamespace cacheNamespace, string requestKey, byte[] content, MapHttpCacheMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cacheNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
        ArgumentNullException.ThrowIfNull(content);
        var directory = GetNamespacePath(cacheNamespace);
        Directory.CreateDirectory(directory);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestKey)));
        await File.WriteAllBytesAsync(Path.Combine(directory, key + ".data"), content, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, key + ".json"), JsonSerializer.Serialize(metadata), cancellationToken).ConfigureAwait(false);
        EnforceBudget();
    }

    /// <summary>Reads an entry when present.</summary>
    public async Task<(byte[] Content, MapHttpCacheMetadata Metadata)?> ReadAsync(MapCacheNamespace cacheNamespace, string requestKey, CancellationToken cancellationToken = default)
    {
        var directory = GetNamespacePath(cacheNamespace);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestKey)));
        var dataPath = Path.Combine(directory, key + ".data");
        var metadataPath = Path.Combine(directory, key + ".json");
        if (!File.Exists(dataPath) || !File.Exists(metadataPath))
            return null;
        File.SetLastAccessTimeUtc(dataPath, DateTime.UtcNow);
        var content = await File.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
        var metadata = JsonSerializer.Deserialize<MapHttpCacheMetadata>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
        return metadata is null ? null : (content, metadata);
    }

    /// <summary>Clears one source/product/style namespace.</summary>
    public void Clear(MapCacheNamespace cacheNamespace)
    {
        var path = GetNamespacePath(cacheNamespace);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    /// <summary>Clears every cache namespace belonging to a source.</summary>
    public void ClearSource(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (!Directory.Exists(root))
            return;
        var prefix = new MapCacheNamespace(sourceId, string.Empty, string.Empty).Key.TrimEnd('_');
        foreach (var directory in Directory.EnumerateDirectories(root).Where(path => Path.GetFileName(path).StartsWith(prefix + "_", StringComparison.Ordinal)))
            Directory.Delete(directory, recursive: true);
    }

    /// <summary>Clears all HTTP cache entries.</summary>
    public void ClearAll()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    /// <summary>Creates cache metadata from standard HTTP headers.</summary>
    public static MapHttpCacheMetadata CreateMetadata(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var expiresAt = response.Content.Headers.Expires;
        if (expiresAt is null && response.Headers.CacheControl?.MaxAge is { } maximumAge)
            expiresAt = DateTimeOffset.UtcNow + maximumAge;
        return new(
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
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(metadata.EntityTag));
        if (metadata.LastModified is not null)
            request.Headers.IfModifiedSince = metadata.LastModified;
    }

    private string GetNamespacePath(MapCacheNamespace value) => Path.Combine(root, value.Key);

    private void EnforceBudget()
    {
        if (!Directory.Exists(root))
            return;
        var files = new DirectoryInfo(root).EnumerateFiles("*.data", SearchOption.AllDirectories).OrderBy(file => file.LastAccessTimeUtc).ToList();
        var size = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (size <= budgetBytes)
                break;
            size -= file.Length;
            file.Delete();
            var metadata = Path.ChangeExtension(file.FullName, ".json");
            if (File.Exists(metadata))
                File.Delete(metadata);
        }
    }
}
