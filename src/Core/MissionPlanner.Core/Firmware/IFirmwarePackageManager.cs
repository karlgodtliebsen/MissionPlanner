namespace MissionPlanner.Core.Firmware;

/// <summary>Downloads, caches, and verifies firmware packages.</summary>
public interface IFirmwarePackageManager
{
    /// <summary>Gets a verified cached package or safely downloads and verifies it.</summary>
    /// <param name="release">The selected manifest release.</param>
    /// <param name="progress">Optional byte-download progress from zero to one.</param>
    /// <param name="cancellationToken">A token that cancels download and verification.</param>
    /// <returns>The verified package.</returns>
    Task<FirmwarePackage> PrepareAsync(
        FirmwareManifestEntry release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
