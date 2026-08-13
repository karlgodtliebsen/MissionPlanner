using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Loads, caches, and deterministically filters firmware releases.</summary>
public sealed class FirmwareCatalogService(
    IFirmwareManifestClient client,
    IFirmwareManifestParser parser,
    IFirmwareCatalogCache cache,
    IOptions<FirmwareOptions> options,
    TimeProvider timeProvider,
    ILogger<FirmwareCatalogService> logger) : IFirmwareCatalogService
{
    /// <inheritdoc />
    public async Task<FirmwareCatalog> GetCatalogAsync(FirmwareCatalogRequest request, CancellationToken cancellationToken = default)
    {
        Debug.Print("GetCatalogAsync");

        ArgumentNullException.ThrowIfNull(request);
        var cached = await cache.GetAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var fresh = cached is not null && now - cached.RetrievedAt <= options.Value.CatalogCacheDuration;
        CachedFirmwareManifest selected;
        FirmwareManifestParseResult? parsed = null;
        var stale = false;
        if (fresh && !request.ForceRefresh)
        {
            Debug.Print("GetCatalogAsync fresh && !request.ForceRefresh");

            selected = cached!;
        }
        else
        {
            try
            {
                Debug.Print("GetCatalogAsync Http request");

                var response = await client.GetAsync(options.Value.ManifestUri, cached, cancellationToken).ConfigureAwait(false);

                selected = response.NotModified && cached is not null
                    ? cached with { RetrievedAt = now }
                    : new CachedFirmwareManifest(response.Content.ToArray(), now, response.ETag, response.LastModified, options.Value.ManifestUri);

                // Validate once before caching and reuse this result below. Parsing large
                // manifests twice also duplicated every malformed-entry diagnostic.
                parsed = parser.ParseWithDiagnostics(selected.Content);

                await cache.SetAsync(selected, cancellationToken).ConfigureAwait(false);
                Debug.Print("GetCatalogAsync cache SetAsync request");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (cached is not null)
            {
                Debug.Print("Manifest refresh failed; using stale cache from {0}.", cached.RetrievedAt);
                logger.LogWarning(exception, "Manifest refresh failed; using stale cache from {RetrievedAt}.", cached.RetrievedAt);
                selected = cached;
                stale = true;
            }
            catch (Exception exception)
            {
                Debug.Print("Manifest retrieval failed and no valid cache is available.. {0}", exception.Message);
                throw new FirmwareManifestException("Manifest retrieval failed and no valid cache is available.", exception);
            }
        }

        parsed ??= parser.ParseWithDiagnostics(selected.Content);
        logger.LogDebug(
            "Firmware manifest processed: {TotalEntries} rows, {AcceptedEntries} accepted, {SkippedEntries} skipped. Reasons: {SkipReasons}",
            parsed.Diagnostics.TotalEntries,
            parsed.Diagnostics.AcceptedEntries,
            parsed.Diagnostics.SkippedEntries,
            string.Join(", ", parsed.Diagnostics.SkipReasons.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));

        IEnumerable<FirmwareManifestEntry> entries = parsed.Entries;
        if (request.VehicleType is { } vehicle)
        {
            entries = entries.Where(entry => entry.Target.VehicleType == vehicle);
        }

        if (request.Channel is { } channel)
        {
            entries = entries.Where(entry => entry.Channel == channel);
        }

        if (request.BoardId is { } board)
        {
            entries = entries.Where(entry => entry.Target.BoardId == board);
        }

        if (request.UsbIdentifier is { } usb)
        {
            entries = entries.Where(entry => entry.Target.UsbIdentifiers.Contains(usb));
        }

        return new FirmwareCatalog(entries.ToArray(), selected.RetrievedAt, stale, parsed.Diagnostics);
    }
}
