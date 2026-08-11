namespace MissionPlanner.Maps.Catalog;

/// <summary>Describes a map provider organization.</summary>
/// <param name="Id">Stable provider identifier.</param>
/// <param name="DisplayName">User-facing provider name.</param>
/// <param name="OrganizationUri">Optional provider website.</param>
public sealed record MapProviderDefinition(string Id, string DisplayName, Uri? OrganizationUri = null);
