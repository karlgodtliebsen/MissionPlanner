using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Settings;

/// <summary>Provides settings-page metadata for one selectable map source.</summary>
/// <param name="Source">Catalog source definition.</param>
/// <param name="Group">User-facing source group.</param>
/// <param name="ProviderAndProduct">Provider and data-product display text.</param>
/// <param name="Connectivity">Online or offline description.</param>
/// <param name="Rendering">Raster or vector description.</param>
/// <param name="CredentialState">Credential requirement and configured state.</param>
/// <param name="AttributionPreview">Required attribution preview.</param>
/// <param name="CacheBehavior">Allowed cache behavior.</param>
/// <param name="OfflinePackAvailability">Offline-pack availability.</param>
/// <param name="PolicyReviewDate">Policy review date.</param>
/// <param name="TermsUri">Terms or source-details URI.</param>
public sealed record MapSettingsSourceItem(
    MapSourceDefinition Source,
    MapSettingsSourceGroup Group,
    string ProviderAndProduct,
    string Connectivity,
    string Rendering,
    string CredentialState,
    string AttributionPreview,
    string CacheBehavior,
    string OfflinePackAvailability,
    DateOnly PolicyReviewDate,
    Uri? TermsUri)
{
    /// <summary>Gets the user-facing source name.</summary>
    public string DisplayName => Source.DisplayName;

    /// <summary>Gets the stable source identifier.</summary>
    public string Id => Source.Id;
}
