using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Credentials;

/// <summary>Describes whether the credential required by a source is configured.</summary>
/// <param name="Requirement">Credential requirement.</param>
/// <param name="IsConfigured">Whether a secret is present.</param>
public sealed record MapCredentialState(MapCredentialRequirement Requirement, bool IsConfigured);
