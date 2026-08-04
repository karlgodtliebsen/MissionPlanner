using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Retrieves manifests with ETag and Last-Modified conditional requests.</summary>
public sealed class HttpFirmwareManifestClient(HttpClient httpClient, IOptions<FirmwareOptions> options) : IFirmwareManifestClient
{
    /// <inheritdoc />
    public async Task<FirmwareManifestResponse> GetAsync(
        Uri uri,
        CachedFirmwareManifest? cached,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (EntityTagHeaderValue.TryParse(cached?.ETag, out var etag))
        {
            request.Headers.IfNoneMatch.Add(etag);
        }

        request.Headers.IfModifiedSince = cached?.LastModified;
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new FirmwareManifestResponse(ReadOnlyMemory<byte>.Empty, true, cached?.ETag, cached?.LastModified);
        }

        response.EnsureSuccessStatusCode();
        var limit = options.Value.MaximumManifestDownloadBytes;
        if (response.Content.Headers.ContentLength > limit)
        {
            throw new FirmwareManifestException("Firmware manifest download exceeds the configured size limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (content.Length + read > limit)
            {
                throw new FirmwareManifestException("Firmware manifest download exceeds the configured size limit.");
            }

            content.Write(buffer, 0, read);
        }

        return new FirmwareManifestResponse(content.ToArray(), false, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
    }
}
