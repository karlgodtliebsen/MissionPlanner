using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Maps.Http;

/// <summary>Describes one reviewed map HTTP request.</summary>
/// <param name="Source">Resolved source and effective policy.</param>
/// <param name="Uri">Secret-free request URI.</param>
/// <param name="ResourceKind">Resource category.</param>
/// <param name="CacheKey">Stable cache key within the source namespace.</param>
/// <param name="Headers">Optional reviewed non-secret headers.</param>
public sealed record MapHttpFetchRequest(
    ResolvedMapSource Source,
    Uri Uri,
    MapHttpResourceKind ResourceKind,
    string CacheKey,
    IReadOnlyDictionary<string, string>? Headers = null);
