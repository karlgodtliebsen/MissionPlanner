using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Downloads;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Downloads and atomically stores bounded official Intel HEX artifacts.</summary>
public interface IDfuHexArtifactDownloader
{
    /// <summary>Downloads or reuses and inspects one official Intel HEX artifact.</summary>
    Task<DfuArtifact> DownloadAsync(Uri sourceUri, string platform, int? boardId, CancellationToken cancellationToken = default);
}

/// <summary>Indicates that a requested DFU artifact could not be safely resolved.</summary>
public sealed class DfuArtifactResolutionException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Streams official Intel HEX artifacts through bounded inspection into the shared atomic cache.</summary>
public sealed class DfuHexArtifactDownloader(
    HttpClient httpClient,
    IFirmwareArtifactStore store,
    IIntelHexInspector inspector,
    IOptions<DfuOptions> options,
    TimeProvider timeProvider) : IDfuHexArtifactDownloader
{
    /// <inheritdoc />
    public async Task<DfuArtifact> DownloadAsync(Uri sourceUri, string platform, int? boardId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!IsTrustedHttps(sourceUri)) throw new DfuArtifactResolutionException("Official DFU artifacts require a configured trusted HTTPS host.");
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"dfu-hex|{sourceUri.AbsoluteUri}"))).ToLowerInvariant();
        if (await store.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            try
            {
                if (!IsTrustedHttps(cached.Metadata.SourceUri)) throw new DfuArtifactResolutionException("The cached artifact source is no longer trusted.");
                return await InspectStoredAsync(cached, cached.Metadata.SourceUri, platform, boardId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or DfuArtifactResolutionException)
            {
                await store.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            }
        }

        await using var writer = await store.CreateTemporaryAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await httpClient.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var finalUri = response.RequestMessage?.RequestUri ?? sourceUri;
            if (!IsTrustedHttps(finalUri)) throw new DfuArtifactResolutionException("The official artifact redirected to an untrusted host.");
            var maximum = options.Value.MaximumIntelHexSourceBytes;
            if (response.Content.Headers.ContentLength is > 0 and var declared && declared > maximum)
                throw new DfuArtifactResolutionException("The official Intel HEX artifact exceeds the configured size limit.");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total = checked(total + read);
                if (total > maximum) throw new DfuArtifactResolutionException("The official Intel HEX artifact exceeded the configured size limit while streaming.");
                await writer.Stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            writer.Stream.Position = 0;
            var inspected = await inspector.InspectAsync(writer.Stream, cancellationToken).ConfigureAwait(false);
            var storedMetadata = new FirmwareArtifactMetadata(cacheKey, finalUri, timeProvider.GetUtcNow(), total, inspected.Sha256);
            var stored = await writer.CommitAsync(storedMetadata, cancellationToken).ConfigureAwait(false);
            return await InspectStoredAsync(stored, finalUri, platform, boardId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (DfuArtifactResolutionException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or OverflowException)
        {
            throw new DfuArtifactResolutionException("The official Intel HEX artifact could not be downloaded and validated.", exception);
        }
    }

    private async Task<DfuArtifact> InspectStoredAsync(IFirmwareStoredArtifact stored, Uri sourceUri, string platform, int? boardId, CancellationToken cancellationToken)
    {
        if (stored.LocalPath is null || !File.Exists(stored.LocalPath) || !string.Equals(Path.GetExtension(stored.LocalPath), ".hex", StringComparison.OrdinalIgnoreCase))
            throw new DfuArtifactResolutionException("The artifact cache did not provide a provider-readable HEX path.");
        await using var stream = await stored.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        var metadata = await inspector.InspectAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(metadata.Sha256, stored.Metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new DfuArtifactResolutionException("The cached Intel HEX artifact hash is invalid.");
        return new DfuArtifact(Path.GetFileName(sourceUri.AbsolutePath), stored.LocalPath, metadata, sourceUri, platform, boardId);
    }

    private bool IsTrustedHttps(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        options.Value.OfficialFirmwareHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);
}
