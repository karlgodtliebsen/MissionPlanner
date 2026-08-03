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

/// <summary>Abstracts temporary and reusable artifact storage.</summary>
public interface IFirmwareArtifactStore
{
    /// <summary>Gets a previously committed artifact by immutable key.</summary>
    Task<IFirmwareStoredArtifact?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default);
    /// <summary>Creates a temporary writer invisible to cache readers.</summary>
    Task<IFirmwareArtifactWriter> CreateTemporaryAsync(string cacheKey, CancellationToken cancellationToken = default);
}

/// <summary>Represents an atomically committed artifact.</summary>
public interface IFirmwareStoredArtifact
{
    /// <summary>Gets immutable artifact metadata.</summary>
    FirmwareArtifactMetadata Metadata { get; }
    /// <summary>Opens a new read stream.</summary>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Owns a partial artifact and deletes it unless committed.</summary>
public interface IFirmwareArtifactWriter : IAsyncDisposable
{
    /// <summary>Gets the temporary writable stream.</summary>
    Stream Stream { get; }
    /// <summary>Atomically publishes the completed artifact.</summary>
    Task<IFirmwareStoredArtifact> CommitAsync(FirmwareArtifactMetadata metadata, CancellationToken cancellationToken = default);
}
