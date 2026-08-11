using System.Text.Json;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Maps.Feed;

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
        {
            throw new InvalidDataException("Map pack feeds must use HTTPS.");
        }

        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(feedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumFeedBytes)
        {
            throw new InvalidDataException("Map pack feed exceeds the size limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var bounded = new MemoryStream();
        await CopyBoundedAsync(stream, bounded, MaximumFeedBytes, null, cancellationToken, false).ConfigureAwait(false);
        var feed = JsonSerializer.Deserialize<SignedMapPackFeed>(bounded.ToArray(), JsonOptions)
                   ?? throw new InvalidDataException("Map pack feed is empty.");
        byte[] signature;
        try { signature = Convert.FromBase64String(feed.Signature); }
        catch (FormatException exception) { throw new InvalidDataException("Map pack feed signature is malformed.", exception); }

        var canonical = SerializePayload(feed.Payload);
        if (!signatureVerifier.Verify(canonical, signature))
        {
            throw new InvalidDataException("Map pack feed signature is invalid.");
        }

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
            {
                break;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("Downloaded map pack exceeds its declared size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            progress?.Report((double)total / maximumBytes);
        }

        if (requireExactLength && total != maximumBytes)
        {
            throw new InvalidDataException("Downloaded map pack is incomplete.");
        }
    }

    private static void Validate(MapPackFeedPayload payload)
    {
        if (payload.SchemaVersion != 1 || string.IsNullOrWhiteSpace(payload.FeedVersion) || payload.Entries is null)
        {
            throw new InvalidDataException("Unsupported or malformed map pack feed.");
        }

        foreach (var entry in payload.Entries)
        {
            if (entry.DownloadUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException("Map pack artifact URIs must use HTTPS.");
            }

            if (entry.DownloadUri.AbsolutePath.Contains("{", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(entry.SourceId) || string.IsNullOrWhiteSpace(entry.ProductId))
            {
                throw new InvalidDataException("Pack entries must reference reviewed artifacts, not tile templates.");
            }

            if (entry.Manifest.SizeBytes <= 0 || string.IsNullOrWhiteSpace(entry.Manifest.Sha256))
            {
                throw new InvalidDataException("Pack integrity metadata is required.");
            }
        }
    }
}
