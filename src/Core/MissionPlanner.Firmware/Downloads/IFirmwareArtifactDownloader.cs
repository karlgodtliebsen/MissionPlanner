using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Downloads;

/// <summary>Downloads, verifies, parses, and atomically stores firmware artifacts.</summary>
public interface IFirmwareArtifactDownloader
{
    /// <summary>Downloads or reuses a validated immutable artifact.</summary>
    Task<DownloadedFirmwareArtifact> DownloadAsync(
        FirmwareArtifact artifact,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
