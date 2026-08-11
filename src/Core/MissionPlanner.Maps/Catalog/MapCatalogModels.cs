namespace MissionPlanner.Maps.Catalog;

/// <summary>Identifies how a map source is accessed.</summary>
public enum MapAccessKind
{
    /// <summary>An HTTP XYZ tile endpoint.</summary>
    HttpXyz,
    /// <summary>An HTTP TMS tile endpoint.</summary>
    HttpTms,
    /// <summary>A Web Map Tile Service endpoint.</summary>
    Wmts,
    /// <summary>A Web Map Service endpoint.</summary>
    Wms,
    /// <summary>A locally installed archive.</summary>
    LocalArchive,
    /// <summary>A locally installed tile directory.</summary>
    LocalDirectory,
    /// <summary>An intentionally blank map.</summary>
    Blank
}

/// <summary>Identifies an archive container format.</summary>
public enum MapArchiveFormat
{
    /// <summary>No archive is used.</summary>
    None,
    /// <summary>An MBTiles SQLite archive.</summary>
    MbTiles,
    /// <summary>A PMTiles archive.</summary>
    PmTiles
}

/// <summary>Identifies the content stored by a map source.</summary>
public enum MapTileContentFormat
{
    /// <summary>PNG raster tiles.</summary>
    RasterPng,
    /// <summary>JPEG raster tiles.</summary>
    RasterJpeg,
    /// <summary>WebP raster tiles.</summary>
    RasterWebp,
    /// <summary>Mapbox vector tiles.</summary>
    VectorMvt
}

/// <summary>Identifies the credential required to access a source.</summary>
public enum MapCredentialRequirement
{
    /// <summary>No credential is required.</summary>
    None,
    /// <summary>An API key is required.</summary>
    ApiKey,
    /// <summary>An OAuth bearer token is required.</summary>
    OAuthToken,
    /// <summary>A user name and password are required.</summary>
    UserNamePassword
}

/// <summary>Identifies how a reviewed provider credential is attached to a request.</summary>
public enum MapAuthenticationStrategy
{
    /// <summary>No authentication is added.</summary>
    None,
    /// <summary>An API key is added as a query parameter.</summary>
    QueryApiKey,
    /// <summary>A bearer token is added to the Authorization header.</summary>
    AuthorizationBearer,
    /// <summary>An API key is added to a reviewed request header.</summary>
    HeaderApiKey
}

/// <summary>Describes a map provider organization.</summary>
/// <param name="Id">Stable provider identifier.</param>
/// <param name="DisplayName">User-facing provider name.</param>
/// <param name="OrganizationUri">Optional provider website.</param>
public sealed record MapProviderDefinition(string Id, string DisplayName, Uri? OrganizationUri = null);

/// <summary>Describes a provider's logical map product.</summary>
/// <param name="Id">Stable product identifier.</param>
/// <param name="ProviderId">Owning provider identifier.</param>
/// <param name="DisplayName">User-facing product name.</param>
/// <param name="Description">Optional product description.</param>
public sealed record MapDataProductDefinition(string Id, string ProviderId, string DisplayName, string? Description = null);

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

/// <summary>Describes an attribution requirement.</summary>
/// <param name="Id">Stable attribution identifier.</param>
/// <param name="Text">Required attribution text.</param>
/// <param name="Uri">Optional attribution link.</param>
/// <param name="RequiredOnMap">Whether the text is required on interactive maps.</param>
/// <param name="RequiredOnExport">Whether the text is required on exports.</param>
public sealed record MapAttributionEntry(string Id, string Text, Uri? Uri, bool RequiredOnMap, bool RequiredOnExport);

/// <summary>Defines one selectable map source.</summary>
/// <param name="Id">Stable source identifier.</param>
/// <param name="ProductId">Logical product identifier.</param>
/// <param name="DisplayName">User-facing source name.</param>
/// <param name="AccessKind">Source access mechanism.</param>
/// <param name="ArchiveFormat">Archive container format.</param>
/// <param name="ContentFormat">Tile content format.</param>
/// <param name="UriTemplate">Optional endpoint URI template.</param>
/// <param name="MinimumZoom">Minimum supported zoom.</param>
/// <param name="MaximumZoom">Maximum supported zoom.</param>
/// <param name="PolicyId">Usage policy identifier.</param>
/// <param name="AttributionIds">Attribution identifiers.</param>
/// <param name="CredentialRequirement">Required credential type.</param>
/// <param name="Capabilities">Supported operations.</param>
/// <param name="IsEnabledByDefault">Whether the source is initially enabled.</param>
/// <param name="IsFutureCandidate">Whether the source is catalogued for later work.</param>
/// <param name="AuthenticationStrategy">Reviewed request authentication strategy.</param>
/// <param name="AuthenticationName">Reviewed query parameter, header name, or authorization scheme.</param>
public sealed record MapSourceDefinition(
    string Id,
    string ProductId,
    string DisplayName,
    MapAccessKind AccessKind,
    MapArchiveFormat ArchiveFormat,
    MapTileContentFormat ContentFormat,
    string? UriTemplate,
    int MinimumZoom,
    int MaximumZoom,
    string PolicyId,
    string[] AttributionIds,
    MapCredentialRequirement CredentialRequirement,
    MapSourceCapabilities Capabilities,
    bool IsEnabledByDefault,
    bool IsFutureCandidate,
    MapAuthenticationStrategy AuthenticationStrategy = MapAuthenticationStrategy.None,
    string? AuthenticationName = null);

/// <summary>Contains versioned map provider, product, policy, attribution, and source definitions.</summary>
/// <param name="SchemaVersion">Catalog schema version.</param>
/// <param name="CatalogVersion">Catalog content version.</param>
/// <param name="Providers">Provider definitions.</param>
/// <param name="Products">Product definitions.</param>
/// <param name="Policies">Usage policies.</param>
/// <param name="Attributions">Attribution entries.</param>
/// <param name="Sources">Map source definitions.</param>
public sealed record MapCatalog(
    int SchemaVersion,
    string CatalogVersion,
    MapProviderDefinition[] Providers,
    MapDataProductDefinition[] Products,
    MapUsagePolicy[] Policies,
    MapAttributionEntry[] Attributions,
    MapSourceDefinition[] Sources);
