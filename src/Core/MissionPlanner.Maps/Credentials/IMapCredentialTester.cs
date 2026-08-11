using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Credentials;

/// <summary>Tests a map credential without exposing it to UI state.</summary>
public interface IMapCredentialTester
{
    /// <summary>Tests a credential for a source.</summary>
    ValueTask<bool> TestAsync(MapSourceDefinition source, string credential, CancellationToken cancellationToken = default);
}
