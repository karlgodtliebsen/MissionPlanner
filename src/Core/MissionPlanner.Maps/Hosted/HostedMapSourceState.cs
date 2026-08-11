using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Hosted;

/// <summary>Describes availability and reviewed policy for a hosted source.</summary>
/// <param name="Source">Catalog source.</param>
/// <param name="IsCredentialConfigured">Whether its required secret is present.</param>
/// <param name="IsSelectable">Whether it may currently be selected.</param>
/// <param name="PolicySummary">Compact reviewed-policy summary.</param>
/// <param name="Attributions">Required attribution.</param>
public sealed record HostedMapSourceState(
    MapSourceDefinition Source,
    bool IsCredentialConfigured,
    bool IsSelectable,
    string PolicySummary,
    IReadOnlyList<MapAttributionEntry> Attributions);
