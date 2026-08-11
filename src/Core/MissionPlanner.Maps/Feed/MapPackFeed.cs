using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.Maps.Feed;

/// <summary>Describes one Mission Planner-reviewed pack artifact.</summary>
/// <param name="Manifest">Offline-pack manifest and integrity metadata.</param>
/// <param name="SourceId">Reviewed source identifier.</param>
/// <param name="ProductId">Reviewed data-product identifier.</param>
/// <param name="DownloadUri">HTTPS artifact URI; never a hosted tile template.</param>
/// <param name="MinimumMissionPlannerVersion">Minimum compatible Mission Planner version.</param>
/// <param name="MinimumRendererVersion">Minimum compatible renderer version.</param>
/// <param name="NoticeUris">License, provenance, or notice references.</param>
public sealed record MapPackFeedEntry(
    OfflineMapPackManifest Manifest,
    string SourceId,
    string ProductId,
    Uri DownloadUri,
    string MinimumMissionPlannerVersion,
    string MinimumRendererVersion,
    Uri[] NoticeUris);

/// <summary>Contains the signed content of a reviewed pack feed.</summary>
/// <param name="SchemaVersion">Feed schema version.</param>
/// <param name="FeedVersion">Monotonic feed content version.</param>
/// <param name="PublishedAt">Feed publication timestamp.</param>
/// <param name="Entries">Reviewed pack artifacts.</param>
public sealed record MapPackFeedPayload(int SchemaVersion, string FeedVersion, DateTimeOffset PublishedAt, MapPackFeedEntry[] Entries);

/// <summary>Wraps canonical feed content with its detached signature.</summary>
/// <param name="Payload">Signed feed content.</param>
/// <param name="Signature">Base64 signature over canonical UTF-8 payload JSON.</param>
public sealed record SignedMapPackFeed(MapPackFeedPayload Payload, string Signature);

/// <summary>Verifies a reviewed map-pack feed signature.</summary>
public interface IMapPackFeedSignatureVerifier
{
    /// <summary>Verifies canonical payload bytes against a detached signature.</summary>
    bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature);
}

/// <summary>Verifies feed signatures using a pinned ECDSA public key.</summary>
public sealed class EcdsaMapPackFeedSignatureVerifier : IMapPackFeedSignatureVerifier, IDisposable
{
    private readonly ECDsa algorithm = ECDsa.Create();

    /// <summary>Initializes a verifier from a PEM-encoded public key.</summary>
    public EcdsaMapPackFeedSignatureVerifier(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        algorithm.ImportFromPem(publicKeyPem);
    }

    /// <inheritdoc />
    public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature) =>
        algorithm.VerifyData(payload, signature, HashAlgorithmName.SHA256);

    /// <inheritdoc />
    public void Dispose() => algorithm.Dispose();
}

/// <summary>Loads and validates versioned signed map-pack feeds.</summary>
public sealed class MapPackFeedClient(IMapHttpClientFactory httpClientFactory, IMapPackFeedSignatureVerifier signatureVerifier)
{
    /// <summary>Maximum accepted feed document size.</summary>
    public const int MaximumFeedBytes = 2 * 1024 * 1024;

    /// <summary>Downloads, verifies, and validates a feed.</summary>
    public async ValueTask<MapPackFeedPayload> GetAsync(Uri feedUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedUri);
        if (feedUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Map pack feeds must use HTTPS.");
        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(feedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumFeedBytes)
            throw new InvalidDataException("Map pack feed exceeds the size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var bounded = new MemoryStream();
        await CopyBoundedAsync(stream, bounded, MaximumFeedBytes, null, cancellationToken, requireExactLength: false).ConfigureAwait(false);
        var feed = JsonSerializer.Deserialize<SignedMapPackFeed>(bounded.ToArray(), JsonOptions)
                   ?? throw new InvalidDataException("Map pack feed is empty.");
        byte[] signature;
        try { signature = Convert.FromBase64String(feed.Signature); }
        catch (FormatException exception) { throw new InvalidDataException("Map pack feed signature is malformed.", exception); }
        var canonical = SerializePayload(feed.Payload);
        if (!signatureVerifier.Verify(canonical, signature))
            throw new InvalidDataException("Map pack feed signature is invalid.");
        Validate(feed.Payload);
        return feed.Payload;
    }

    /// <summary>Serializes payload content deterministically for signing or verification.</summary>
    public static byte[] SerializePayload(MapPackFeedPayload payload) => JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    internal static async Task CopyBoundedAsync(Stream source, Stream destination, long maximumBytes, IProgress<double>? progress, CancellationToken cancellationToken, bool requireExactLength = true)
    {
        var buffer = new byte[81_920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
            if (total > maximumBytes)
                throw new InvalidDataException("Downloaded map pack exceeds its declared size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            progress?.Report((double)total / maximumBytes);
        }
        if (requireExactLength && total != maximumBytes)
            throw new InvalidDataException("Downloaded map pack is incomplete.");
    }

    private static void Validate(MapPackFeedPayload payload)
    {
        if (payload.SchemaVersion != 1 || string.IsNullOrWhiteSpace(payload.FeedVersion) || payload.Entries is null)
            throw new InvalidDataException("Unsupported or malformed map pack feed.");
        foreach (var entry in payload.Entries)
        {
            if (entry.DownloadUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("Map pack artifact URIs must use HTTPS.");
            if (entry.DownloadUri.AbsolutePath.Contains("{", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(entry.SourceId) || string.IsNullOrWhiteSpace(entry.ProductId))
                throw new InvalidDataException("Pack entries must reference reviewed artifacts, not tile templates.");
            if (entry.Manifest.SizeBytes <= 0 || string.IsNullOrWhiteSpace(entry.Manifest.Sha256))
                throw new InvalidDataException("Pack integrity metadata is required.");
        }
    }
}

/// <summary>Downloads and atomically installs compatible artifacts from an approved feed.</summary>
public sealed class MapPackFeedInstaller(
    IMapHttpClientFactory httpClientFactory,
    IOfflineMapPackInstaller installer,
    IOfflineMapPackRepository repository)
{
    /// <summary>Downloads and installs an upgrade, retaining the working version on failure.</summary>
    public async ValueTask<InstalledOfflineMapPack> InstallAsync(
        MapPackFeedEntry entry,
        Version missionPlannerVersion,
        Version rendererVersion,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureCompatible(entry, missionPlannerVersion, rendererVersion);
        var installed = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        var existing = installed.Where(value => value.Manifest.Id == entry.Manifest.Id).ToArray();
        if (existing.Any(value => CompareVersion(value.Manifest.Version, entry.Manifest.Version) >= 0))
            throw new InvalidOperationException("Map pack downgrade or duplicate installation is not allowed.");

        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(entry.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && length != entry.Manifest.SizeBytes)
            throw new InvalidDataException("Map pack response size does not match its signed manifest.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var bounded = new MemoryStream(checked((int)Math.Min(entry.Manifest.SizeBytes, int.MaxValue)));
        await MapPackFeedClient.CopyBoundedAsync(source, bounded, entry.Manifest.SizeBytes, progress, cancellationToken).ConfigureAwait(false);
        bounded.Position = 0;
        var result = await installer.InstallAsync(entry.Manifest, bounded, cancellationToken).ConfigureAwait(false);
        foreach (var prior in existing)
            await repository.RemoveAsync(prior.Manifest.Id, prior.Manifest.Version, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static void EnsureCompatible(MapPackFeedEntry entry, Version app, Version renderer)
    {
        if (!Version.TryParse(entry.MinimumMissionPlannerVersion, out var minimumApp) || !Version.TryParse(entry.MinimumRendererVersion, out var minimumRenderer))
            throw new InvalidDataException("Pack compatibility versions are malformed.");
        if (app < minimumApp || renderer < minimumRenderer)
            throw new NotSupportedException("Map pack requires a newer Mission Planner or renderer version.");
    }

    private static int CompareVersion(string left, string right) =>
        Version.TryParse(left, out var leftVersion) && Version.TryParse(right, out var rightVersion)
            ? leftVersion.CompareTo(rightVersion)
            : StringComparer.Ordinal.Compare(left, right);
}
