using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Policy;

/// <summary>Default fail-closed map policy evaluator.</summary>
public sealed class MapPolicyEvaluator : IMapPolicyEvaluator
{
    /// <inheritdoc />
    public MapPolicyDecision Evaluate(MapSourceDefinition source, MapUsagePolicy policy, MapOperation operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);
        if (!StringComparer.Ordinal.Equals(source.PolicyId, policy.Id))
        {
            return new MapPolicyDecision(operation, false, policy.Id, $"Source policy '{source.PolicyId}' does not match supplied policy '{policy.Id}'.");
        }

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
            var _ => false
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
            var _ => false
        };

        if (!capable)
        {
            return new MapPolicyDecision(operation, false, policy.Id, "The source does not declare this technical capability.");
        }

        if (!permitted)
        {
            return new MapPolicyDecision(operation, false, policy.Id, "The reviewed provider policy does not permit this operation.");
        }

        return new MapPolicyDecision(operation, true, policy.Id, "Technical capability and reviewed provider policy both allow this operation.");
    }
}
