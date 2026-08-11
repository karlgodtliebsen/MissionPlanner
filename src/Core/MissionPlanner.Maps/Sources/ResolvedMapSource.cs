using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;

namespace MissionPlanner.Maps.Sources;

/// <summary>Contains renderer-neutral data needed to create a runtime source.</summary>
/// <param name="Id">Stable selected source identifier.</param>
/// <param name="Origin">Source origin.</param>
/// <param name="Provider">Owning provider.</param>
/// <param name="DataProduct">Owning data product.</param>
/// <param name="Definition">Effective source definition.</param>
/// <param name="EffectivePolicy">Reviewed effective policy.</param>
/// <param name="Attribution">Required attribution entries.</param>
/// <param name="CredentialState">Credential requirement and configuration state.</param>
/// <param name="Location">Endpoint template or local archive path.</param>
public sealed record ResolvedMapSource(
    string Id,
    MapSourceOrigin Origin,
    MapProviderDefinition Provider,
    MapDataProductDefinition DataProduct,
    MapSourceDefinition Definition,
    MapUsagePolicy EffectivePolicy,
    IReadOnlyList<MapAttributionEntry> Attribution,
    MapCredentialState CredentialState,
    string? Location);
