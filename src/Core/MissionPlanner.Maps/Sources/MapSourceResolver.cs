using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Custom;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.Maps.Sources;

/// <summary>Default catalog, offline-pack, and custom-source resolver.</summary>
public sealed class MapSourceResolver(
    IMapCatalog catalogService,
    IMapPolicyEvaluator policyEvaluator,
    IMapSecretStore secretStore,
    IOfflineMapPackRepository offlinePacks,
    ICustomMapSourceStore customSources) : IMapSourceResolver
{
    private readonly MapCatalog catalog = catalogService.Current;

    /// <inheritdoc />
    public async ValueTask<MapSourceResolutionResult> ResolveAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return Failure(MapSourceResolutionStatus.UnknownSource, "A map source identifier is required.");
            }

            if (sourceId.StartsWith("pack:", StringComparison.Ordinal))
            {
                return await ResolvePackAsync(sourceId, cancellationToken).ConfigureAwait(false);
            }

            if (sourceId.StartsWith("custom:", StringComparison.Ordinal))
            {
                return await ResolveCustomAsync(sourceId, cancellationToken).ConfigureAwait(false);
            }

            return await ResolveCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(MapSourceResolutionStatus.Cancelled, "Map source resolution was cancelled.");
        }
    }

    private async ValueTask<MapSourceResolutionResult> ResolveCatalogAsync(string sourceId, CancellationToken cancellationToken)
    {
        var definition = catalog.Sources.SingleOrDefault(source => source.Id == sourceId);
        if (definition is null)
        {
            return Failure(MapSourceResolutionStatus.UnknownSource, $"Map source '{sourceId}' is unknown.");
        }

        if (definition.IsFutureCandidate)
        {
            return Failure(MapSourceResolutionStatus.Deferred, $"Map source '{sourceId}' is deferred by the current renderer decision.");
        }

        if (!definition.IsEnabledByDefault && definition.CredentialRequirement == MapCredentialRequirement.None)
        {
            return Failure(MapSourceResolutionStatus.Disabled, $"Map source '{sourceId}' is disabled.");
        }

        return await CompleteAsync(sourceId, MapSourceOrigin.Catalog, definition, definition.UriTemplate, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<MapSourceResolutionResult> ResolvePackAsync(string sourceId, CancellationToken cancellationToken)
    {
        var parts = sourceId.Split(':', 3);
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return Failure(MapSourceResolutionStatus.InvalidDefinition, "Pack source IDs must use pack:<id>:<version>.");
        }

        var pack = await offlinePacks.FindAsync(parts[1], parts[2], cancellationToken).ConfigureAwait(false);
        if (pack is null || !File.Exists(pack.ArchivePath))
        {
            return Failure(MapSourceResolutionStatus.PackMissing, $"Installed map pack '{parts[1]}' version '{parts[2]}' is unavailable.");
        }

        var template = catalog.Sources.Single(source => source.Id == "raster-mbtiles-template");
        var format = pack.Manifest.RasterFormat.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => MapTileContentFormat.RasterJpeg,
            "webp" => MapTileContentFormat.RasterWebp,
            var _ => MapTileContentFormat.RasterPng
        };
        var definition = template with
        {
            Id = sourceId,
            DisplayName = pack.Manifest.DisplayName,
            MinimumZoom = pack.Manifest.MinimumZoom,
            MaximumZoom = pack.Manifest.MaximumZoom,
            ContentFormat = format,
            AttributionIds = [],
            IsEnabledByDefault = true,
            IsFutureCandidate = false
        };
        var attribution = string.IsNullOrWhiteSpace(pack.Manifest.Attribution)
            ? Array.Empty<MapAttributionEntry>()
            : [new MapAttributionEntry($"pack:{parts[1]}", pack.Manifest.Attribution, null, true, true)];
        return Complete(sourceId, MapSourceOrigin.InstalledPack, definition, pack.ArchivePath, attribution, new MapCredentialState(MapCredentialRequirement.None, true));
    }

    private async ValueTask<MapSourceResolutionResult> ResolveCustomAsync(string sourceId, CancellationToken cancellationToken)
    {
        var id = sourceId["custom:".Length..];
        var settings = (await customSources.LoadAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(source => source.Id == id);
        if (settings is null)
        {
            return Failure(MapSourceResolutionStatus.CustomSourceMissing, $"Custom map source '{id}' is unavailable.");
        }

        if (CustomMapSourceValidator.Validate(settings).Any(issue => !issue.IsWarning))
        {
            return Failure(MapSourceResolutionStatus.InvalidDefinition, $"Custom map source '{id}' is invalid.");
        }

        var template = catalog.Sources.Single(source => source.Id == "custom-raster-template");
        var definition = template with
        {
            Id = sourceId,
            DisplayName = settings.DisplayName,
            AccessKind = settings.AccessKind,
            UriTemplate = settings.Endpoint,
            MinimumZoom = settings.MinimumZoom,
            MaximumZoom = settings.MaximumZoom,
            CredentialRequirement = settings.CredentialRequirement,
            AttributionIds = [],
            IsEnabledByDefault = true,
            IsFutureCandidate = false
        };
        IReadOnlyList<MapAttributionEntry> attribution = [new MapAttributionEntry(sourceId, settings.Attribution, null, true, true)];
        var credential = await CredentialStateAsync(definition, cancellationToken).ConfigureAwait(false);
        if (!credential.IsConfigured)
        {
            return Failure(MapSourceResolutionStatus.CredentialMissing, $"Credentials are not configured for {settings.DisplayName}.");
        }

        return Complete(sourceId, MapSourceOrigin.Custom, definition, settings.Endpoint, attribution, credential);
    }

    private async ValueTask<MapSourceResolutionResult> CompleteAsync(string id, MapSourceOrigin origin, MapSourceDefinition definition, string? location, CancellationToken cancellationToken)
    {
        var credential = await CredentialStateAsync(definition, cancellationToken).ConfigureAwait(false);
        if (!credential.IsConfigured)
        {
            return Failure(MapSourceResolutionStatus.CredentialMissing, $"Credentials are not configured for {definition.DisplayName}.");
        }

        var attribution = definition.AttributionIds.Select(attributionId => catalog.Attributions.Single(item => item.Id == attributionId)).ToArray();
        return Complete(id, origin, definition, location, attribution, credential);
    }

    private MapSourceResolutionResult Complete(string id, MapSourceOrigin origin, MapSourceDefinition definition, string? location, IReadOnlyList<MapAttributionEntry> attribution, MapCredentialState credential)
    {
        if (definition.ContentFormat == MapTileContentFormat.VectorMvt || definition.ArchiveFormat == MapArchiveFormat.PmTiles)
        {
            return Failure(MapSourceResolutionStatus.UnsupportedByRenderer, $"Map source '{id}' is not supported by the current raster renderer.");
        }

        var policy = catalog.Policies.SingleOrDefault(item => item.Id == definition.PolicyId);
        if (policy is null)
        {
            return Failure(MapSourceResolutionStatus.InvalidDefinition, $"Map source '{id}' references an unknown policy.");
        }

        var decision = policyEvaluator.Evaluate(definition, policy, MapOperation.InteractiveUse);
        if (!decision.IsAllowed)
        {
            return Failure(MapSourceResolutionStatus.PolicyDenied, decision.Reason);
        }

        var product = catalog.Products.Single(item => item.Id == definition.ProductId);
        var provider = catalog.Providers.Single(item => item.Id == product.ProviderId);
        return new MapSourceResolutionResult(MapSourceResolutionStatus.None, new ResolvedMapSource(id, origin, provider, product, definition, policy, attribution, credential, location));
    }

    private async ValueTask<MapCredentialState> CredentialStateAsync(MapSourceDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.CredentialRequirement == MapCredentialRequirement.None)
        {
            return new MapCredentialState(definition.CredentialRequirement, true);
        }

        var secret = await secretStore.GetAsync($"maps.credentials.{definition.Id}", cancellationToken).ConfigureAwait(false);
        return new MapCredentialState(definition.CredentialRequirement, !string.IsNullOrWhiteSpace(secret));
    }

    private static MapSourceResolutionResult Failure(MapSourceResolutionStatus status, string message) => new(status, null, message);
}
