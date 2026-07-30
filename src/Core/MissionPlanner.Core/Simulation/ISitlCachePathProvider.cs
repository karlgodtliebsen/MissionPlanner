namespace MissionPlanner.Core.Simulation;

/// <summary>Supplies the platform-local MissionPlanner-owned SITL cache root.</summary>
public interface ISitlCachePathProvider
{
    /// <summary>Gets the absolute cache root.</summary>
    string CacheRoot { get; }
}
