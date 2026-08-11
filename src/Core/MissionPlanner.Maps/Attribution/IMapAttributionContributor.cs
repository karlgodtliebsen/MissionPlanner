using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Contributes attribution for a currently visible source or layer.</summary>
public interface IMapAttributionContributor
{
    /// <summary>Gets a stable contributor identifier.</summary>
    string ContributorId { get; }

    /// <summary>Gets whether this contributor is currently visible.</summary>
    bool IsVisible { get; }

    /// <summary>Gets static attribution entries.</summary>
    IReadOnlyCollection<MapAttributionEntry> Attributions { get; }
}
