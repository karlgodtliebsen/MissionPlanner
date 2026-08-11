namespace MissionPlanner.Maps.Catalog;

/// <summary>Describes operations supported by a map source.</summary>
/// <param name="SupportsInteractiveUse">Whether interactive display is supported.</param>
/// <param name="SupportsOfflineCache">Whether tiles may be cached for offline use.</param>
/// <param name="SupportsPackDownload">Whether bounded pack download is supported.</param>
/// <param name="SupportsExport">Whether imagery may be included in exports.</param>
/// <param name="SupportsPrinting">Whether imagery may be printed.</param>
/// <param name="SupportsBulkPrefetch">Whether bulk prefetch is technically supported.</param>
/// <param name="SupportsProxy">Whether proxying to other clients is technically supported.</param>
/// <param name="SupportsRedistribution">Whether pack redistribution is technically supported.</param>
public sealed record MapSourceCapabilities(
    bool SupportsInteractiveUse,
    bool SupportsOfflineCache,
    bool SupportsPackDownload,
    bool SupportsExport,
    bool SupportsPrinting,
    bool SupportsBulkPrefetch = false,
    bool SupportsProxy = false,
    bool SupportsRedistribution = false);
