using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Policy;

/// <summary>Evaluates the intersection of technical capability and reviewed provider policy.</summary>
public interface IMapPolicyEvaluator
{
    /// <summary>Evaluates an operation for a catalog source.</summary>
    /// <param name="source">Source metadata.</param>
    /// <param name="policy">Reviewed source policy.</param>
    /// <param name="operation">Requested operation.</param>
    /// <returns>The effective decision.</returns>
    MapPolicyDecision Evaluate(MapSourceDefinition source, MapUsagePolicy policy, MapOperation operation);
}
