namespace MissionPlanner.Maps.Catalog;

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
