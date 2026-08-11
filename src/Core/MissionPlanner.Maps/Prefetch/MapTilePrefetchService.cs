using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Maps.Prefetch;

/// <summary>Geographic bounds for cache warming.</summary>
public sealed record MapPrefetchBounds(double South, double West, double North, double East);
/// <summary>Policy-aware cache-warming request.</summary>
public sealed record MapPrefetchRequest(string SourceId, IReadOnlyList<MapPrefetchBounds> Areas, int MinimumZoom, int MaximumZoom);
/// <summary>Preflight estimate for a prefetch request.</summary>
public sealed record MapPrefetchEstimate(bool IsAllowed, string Message, int TileCount, int MinimumZoom, int MaximumZoom);
/// <summary>Completed cache-warming result.</summary>
public sealed record MapPrefetchResult(bool Succeeded, string Message, int CompletedTiles, int TotalTiles);
/// <summary>Warms the reviewed online HTTP cache without creating offline packs.</summary>
public interface IMapTilePrefetchService
{
    /// <summary>Evaluates policy and enumerates unique XYZ tiles.</summary>
    ValueTask<MapPrefetchEstimate> EstimateAsync(MapPrefetchRequest request, CancellationToken cancellationToken = default);
    /// <summary>Fetches approved tiles with bounded concurrency.</summary>
    ValueTask<MapPrefetchResult> PrefetchAsync(MapPrefetchRequest request, IProgress<(int Completed, int Total)>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Default policy-aware XYZ cache warmer.</summary>
public sealed class MapTilePrefetchService(IMapSourceResolver resolver, IMapPolicyEvaluator policies, IMapHttpResourceFetcher fetcher) : IMapTilePrefetchService
{
    private const int HardTileLimit = 10_000;
    /// <inheritdoc />
    public async ValueTask<MapPrefetchEstimate> EstimateAsync(MapPrefetchRequest request, CancellationToken cancellationToken = default)
    {
        var resolution = await resolver.ResolveAsync(request.SourceId, cancellationToken).ConfigureAwait(false);
        if (!resolution.IsSuccess || resolution.Source is null) return new(false, resolution.Message ?? "Map source could not be resolved.", 0, request.MinimumZoom, request.MaximumZoom);
        var source = resolution.Source;
        var decision = policies.Evaluate(source.Definition, source.EffectivePolicy, MapOperation.BulkPrefetch);
        if (!decision.IsAllowed || source.Definition.AccessKind is not MapAccessKind.HttpXyz || string.Equals(source.Id, "osm-standard", StringComparison.OrdinalIgnoreCase))
            return new(false, decision.IsAllowed ? "This source is not an approved online XYZ bulk-prefetch source." : decision.Reason, 0, request.MinimumZoom, request.MaximumZoom);
        var count = Enumerate(request).Take(HardTileLimit + 1).Count();
        return count > HardTileLimit ? new(false, $"Request exceeds the {HardTileLimit} tile safety limit.", count, request.MinimumZoom, request.MaximumZoom)
            : new(true, $"Provider policy '{decision.PolicyId}' allows online HTTP cache warming.", count, request.MinimumZoom, request.MaximumZoom);
    }
    /// <inheritdoc />
    public async ValueTask<MapPrefetchResult> PrefetchAsync(MapPrefetchRequest request, IProgress<(int Completed, int Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        var estimate = await EstimateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!estimate.IsAllowed) return new(false, estimate.Message, 0, estimate.TileCount);
        var resolution = await resolver.ResolveAsync(request.SourceId, cancellationToken).ConfigureAwait(false);
        var source = resolution.Source!; var tiles = Enumerate(request).ToArray(); var completed = 0;
        await Parallel.ForEachAsync(tiles, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (tile, token) =>
        {
            var template = source.Location ?? source.Definition.UriTemplate ?? throw new InvalidOperationException("Tile source has no URI template.");
            var uri = new Uri(template.Replace("{z}", tile.Z.ToString()).Replace("{x}", tile.X.ToString()).Replace("{y}", tile.Y.ToString()));
            _ = await fetcher.FetchAsync(new(source, uri, MapHttpResourceKind.RasterTile, $"{tile.Z}/{tile.X}/{tile.Y}"), token).ConfigureAwait(false);
            var value = Interlocked.Increment(ref completed); progress?.Report((value, tiles.Length));
        }).ConfigureAwait(false);
        return new(true, $"Warmed {completed} cached tiles; no offline pack was created.", completed, tiles.Length);
    }
    private static IEnumerable<(int Z, int X, int Y)> Enumerate(MapPrefetchRequest request)
    {
        var unique = new HashSet<(int, int, int)>();
        foreach (var area in request.Areas) for (var zoom = request.MinimumZoom; zoom <= request.MaximumZoom; zoom++)
        {
            var n = 1 << zoom; var minX = LonToX(area.West, n); var maxX = LonToX(area.East, n); var minY = LatToY(area.North, n); var maxY = LatToY(area.South, n);
            for (var x = minX; x <= maxX; x++) for (var y = minY; y <= maxY; y++) unique.Add((zoom, x, y));
        }
        return unique.OrderBy(tile => tile.Item1).ThenBy(tile => tile.Item2).ThenBy(tile => tile.Item3).Select(tile => (tile.Item1, tile.Item2, tile.Item3));
    }
    private static int LonToX(double longitude, int n) => Math.Clamp((int)Math.Floor((longitude + 180d) / 360d * n), 0, n - 1);
    private static int LatToY(double latitude, int n) { var radians = Math.Clamp(latitude, -85.0511, 85.0511) * Math.PI / 180d; return Math.Clamp((int)Math.Floor((1d - Math.Log(Math.Tan(radians) + 1d / Math.Cos(radians)) / Math.PI) / 2d * n), 0, n - 1); }
}
