namespace MissionPlanner.Maps.Sources;

/// <summary>Resolves all supported stable source namespaces without renderer dependencies.</summary>
public interface IMapSourceResolver
{
    /// <summary>Resolves a stable source identifier.</summary>
    ValueTask<MapSourceResolutionResult> ResolveAsync(string sourceId, CancellationToken cancellationToken = default);
}
