namespace MissionPlanner.Firmware.Dfu;

/// <summary>Resolves official or local Intel HEX artifacts.</summary>
public interface IDfuArtifactResolver
{
    /// <summary>Resolves, downloads when required, and inspects a requested artifact.</summary>
    Task<DfuArtifact> ResolveAsync(DfuInstallationRequest request, CancellationToken cancellationToken = default);
}
