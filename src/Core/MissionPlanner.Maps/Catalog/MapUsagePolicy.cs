namespace MissionPlanner.Maps.Catalog;

/// <summary>Records reviewed usage constraints for a map product.</summary>
/// <param name="Id">Stable policy identifier.</param>
/// <param name="TermsUri">Link to applicable terms.</param>
/// <param name="ReviewedOn">Date on which the policy was reviewed.</param>
/// <param name="ReviewNotes">Human-readable review notes.</param>
/// <param name="AllowInteractiveUse">Whether interactive use is allowed.</param>
/// <param name="AllowOfflineCache">Whether offline caching is allowed.</param>
/// <param name="AllowPackDownload">Whether bounded pack download is allowed.</param>
/// <param name="AllowExport">Whether imagery may be exported.</param>
/// <param name="AllowPrinting">Whether imagery may be printed.</param>
/// <param name="RequiresVisibleAttribution">Whether attribution must remain visible.</param>
/// <param name="AllowBulkPrefetch">Whether reviewed policy allows bulk prefetch.</param>
/// <param name="AllowProxy">Whether reviewed policy allows proxying to other clients.</param>
/// <param name="AllowRedistribution">Whether reviewed policy allows pack redistribution.</param>
public sealed record MapUsagePolicy(
    string Id,
    Uri? TermsUri,
    DateOnly ReviewedOn,
    string ReviewNotes,
    bool AllowInteractiveUse,
    bool AllowOfflineCache,
    bool AllowPackDownload,
    bool AllowExport,
    bool AllowPrinting,
    bool RequiresVisibleAttribution,
    bool AllowBulkPrefetch = false,
    bool AllowProxy = false,
    bool AllowRedistribution = false);
