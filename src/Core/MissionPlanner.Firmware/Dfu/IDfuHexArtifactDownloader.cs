namespace MissionPlanner.Firmware.Dfu;

/// <summary>Downloads and atomically stores bounded official Intel HEX artifacts.</summary>
public interface IDfuHexArtifactDownloader
{
    /// <summary>Downloads or reuses and inspects one official Intel HEX artifact.</summary>
    Task<DfuArtifact> DownloadAsync(Uri sourceUri, string platform, int? boardId, CancellationToken cancellationToken = default);
}
