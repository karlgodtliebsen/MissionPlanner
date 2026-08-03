using System.Net;
using System.Net.Http.Headers;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Retrieves manifests with ETag and Last-Modified conditional requests.</summary>
public sealed class HttpFirmwareManifestClient(HttpClient httpClient) : IFirmwareManifestClient
{
    /// <inheritdoc />
    public async Task<FirmwareManifestResponse> GetAsync(
        Uri uri,
        CachedFirmwareManifest? cached,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (EntityTagHeaderValue.TryParse(cached?.ETag, out var etag)) request.Headers.IfNoneMatch.Add(etag);
        request.Headers.IfModifiedSince = cached?.LastModified;
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new FirmwareManifestResponse(ReadOnlyMemory<byte>.Empty, true, cached?.ETag, cached?.LastModified);
        }

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new FirmwareManifestResponse(content, false, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
    }
}
