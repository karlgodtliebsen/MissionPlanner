namespace MissionPlanner.Maps.Http;

/// <summary>Provides live non-secret HTTP cache settings.</summary>
public interface IMapHttpRuntimeSettings
{
    /// <summary>Gets whether protocol-aware HTTP caching is enabled.</summary>
    bool CacheEnabled { get; }

    /// <summary>Gets the current cache budget in bytes.</summary>
    long CacheLimitBytes { get; }
}
