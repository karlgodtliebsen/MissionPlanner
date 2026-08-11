using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Policy;

/// <summary>Identifies an operation governed by map policy.</summary>
public enum MapOperation
{
    /// <summary>Interactive map display.</summary>
    InteractiveUse,
    /// <summary>A bounded protocol-aware client disk cache.</summary>
    ClientDiskCache,
    /// <summary>An explicit offline area download.</summary>
    OfflineAreaDownload,
    /// <summary>Bulk tile prefetch.</summary>
    BulkPrefetch,
    /// <summary>Proxying content to other clients.</summary>
    Proxy,
    /// <summary>Redistributing a generated or downloaded pack.</summary>
    RedistributedPack,
    /// <summary>Including content in a static export.</summary>
    StaticExport,
    /// <summary>Printing map content.</summary>
    Printing
}

/// <summary>Describes the effective decision for one map operation.</summary>
/// <param name="Operation">Evaluated operation.</param>
/// <param name="IsAllowed">Whether the operation is allowed.</param>
/// <param name="PolicyId">Policy that produced the decision.</param>
/// <param name="Reason">Human-readable decision reason.</param>
public sealed record MapPolicyDecision(MapOperation Operation, bool IsAllowed, string PolicyId, string Reason);

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

/// <summary>Default fail-closed map policy evaluator.</summary>
public sealed class MapPolicyEvaluator : IMapPolicyEvaluator
{
    /// <inheritdoc />
    public MapPolicyDecision Evaluate(MapSourceDefinition source, MapUsagePolicy policy, MapOperation operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);
        if (!StringComparer.Ordinal.Equals(source.PolicyId, policy.Id))
            return new(operation, false, policy.Id, $"Source policy '{source.PolicyId}' does not match supplied policy '{policy.Id}'.");

        var capable = operation switch
        {
            MapOperation.InteractiveUse => source.Capabilities.SupportsInteractiveUse,
            MapOperation.ClientDiskCache => source.Capabilities.SupportsOfflineCache,
            MapOperation.OfflineAreaDownload => source.Capabilities.SupportsPackDownload,
            MapOperation.BulkPrefetch => source.Capabilities.SupportsBulkPrefetch,
            MapOperation.StaticExport => source.Capabilities.SupportsExport,
            MapOperation.Printing => source.Capabilities.SupportsPrinting,
            MapOperation.Proxy => source.Capabilities.SupportsProxy,
            MapOperation.RedistributedPack => source.Capabilities.SupportsRedistribution,
            _ => false
        };
        var permitted = operation switch
        {
            MapOperation.InteractiveUse => policy.AllowInteractiveUse,
            MapOperation.ClientDiskCache => policy.AllowOfflineCache,
            MapOperation.OfflineAreaDownload => policy.AllowPackDownload,
            MapOperation.BulkPrefetch => policy.AllowBulkPrefetch,
            MapOperation.StaticExport => policy.AllowExport,
            MapOperation.Printing => policy.AllowPrinting,
            MapOperation.Proxy => policy.AllowProxy,
            MapOperation.RedistributedPack => policy.AllowRedistribution,
            _ => false
        };

        if (!capable)
            return new(operation, false, policy.Id, "The source does not declare this technical capability.");
        if (!permitted)
            return new(operation, false, policy.Id, "The reviewed provider policy does not permit this operation.");
        return new(operation, true, policy.Id, "Technical capability and reviewed provider policy both allow this operation.");
    }
}
