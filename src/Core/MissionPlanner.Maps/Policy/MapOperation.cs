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
