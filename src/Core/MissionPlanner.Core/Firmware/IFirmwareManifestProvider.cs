namespace MissionPlanner.Core.Firmware;

/// <summary>Discovers firmware releases from an authoritative manifest.</summary>
public interface IFirmwareManifestProvider
{
    /// <summary>Gets the available manifest releases.</summary>
    /// <param name="cancellationToken">A token that cancels discovery.</param>
    /// <returns>The available releases.</returns>
    Task<IReadOnlyList<FirmwareManifestEntry>> GetReleasesAsync(CancellationToken cancellationToken = default);
}
