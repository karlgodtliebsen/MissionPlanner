namespace MissionPlanner.Maps.Catalog;

/// <summary>Describes a provider's logical map product.</summary>
/// <param name="Id">Stable product identifier.</param>
/// <param name="ProviderId">Owning provider identifier.</param>
/// <param name="DisplayName">User-facing product name.</param>
/// <param name="Description">Optional product description.</param>
public sealed record MapDataProductDefinition(string Id, string ProviderId, string DisplayName, string? Description = null);
