using BruTile.Predefined;
using Mapsui.Layers;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.App.Maps;

/// <summary>Creates Mapsui basemap layers from approved catalog sources.</summary>
public interface IMapsuiBasemapFactory
{
    /// <summary>Creates a basemap for a stable source identifier.</summary>
    /// <param name="sourceId">Catalog source identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created basemap layer.</returns>
    ValueTask<ILayer> CreateAsync(string sourceId, CancellationToken cancellationToken = default);
}

/// <summary>Maps the built-in catalog sources to their existing Mapsui/BruTile implementations.</summary>
public sealed class MapsuiBasemapFactory : IMapsuiBasemapFactory
{
    /// <summary>Stable identity assigned to the single basemap slot.</summary>
    public const string BasemapLayerName = "MissionPlanner.Basemap";
    private readonly MapCatalog catalog;
    private readonly IMapPolicyEvaluator policyEvaluator;

    /// <summary>Initializes the built-in Mapsui basemap factory.</summary>
    public MapsuiBasemapFactory() : this(BuiltInMapCatalog.Load(), new MapPolicyEvaluator())
    {
    }

    /// <summary>Initializes a factory with explicit catalog and policy dependencies.</summary>
    public MapsuiBasemapFactory(MapCatalog catalog, IMapPolicyEvaluator policyEvaluator)
    {
        this.catalog = catalog;
        this.policyEvaluator = policyEvaluator;
    }

    /// <inheritdoc />
    public ValueTask<ILayer> CreateAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = catalog.Sources.SingleOrDefault(item => item.Id == sourceId)
            ?? throw new KeyNotFoundException($"Map source '{sourceId}' is not in the catalog.");
        if (!source.IsEnabledByDefault)
            throw new InvalidOperationException($"Map source '{sourceId}' is disabled.");
        var policy = catalog.Policies.Single(item => item.Id == source.PolicyId);
        var decision = policyEvaluator.Evaluate(source, policy, MapOperation.InteractiveUse);
        if (!decision.IsAllowed)
            throw new InvalidOperationException($"Map source '{sourceId}' is denied by policy '{decision.PolicyId}': {decision.Reason}");

        ILayer layer = sourceId switch
        {
            "no-map" => new MemoryLayer(),
            "osm-standard" => OpenStreetMap.CreateTileLayer(),
            "esri-world-topo" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldTopo)),
            "esri-world-physical" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldPhysical)),
            "esri-world-shaded-relief" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldShadedRelief)),
            "esri-world-dark-gray" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldDarkGrayBase)),
            _ => throw new NotSupportedException($"Map source '{sourceId}' has no Mapsui adapter.")
        };
        layer.Name = BasemapLayerName;
        return ValueTask.FromResult(layer);
    }
}
