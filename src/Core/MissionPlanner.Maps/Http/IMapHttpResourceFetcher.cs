namespace MissionPlanner.Maps.Http;

/// <summary>Fetches online map resources through policy, credentials, validators, and cache.</summary>
public interface IMapHttpResourceFetcher
{
    /// <summary>Fetches one reviewed resource.</summary>
    ValueTask<MapHttpFetchResult> FetchAsync(MapHttpFetchRequest request, CancellationToken cancellationToken = default);
}
