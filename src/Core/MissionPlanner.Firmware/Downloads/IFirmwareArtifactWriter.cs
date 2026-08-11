namespace MissionPlanner.Firmware.Downloads;

/// <summary>Owns a partial artifact and deletes it unless committed.</summary>
public interface IFirmwareArtifactWriter : IAsyncDisposable
{
    /// <summary>Gets the temporary writable stream.</summary>
    Stream Stream { get; }

    /// <summary>Atomically publishes the completed artifact.</summary>
    Task<IFirmwareStoredArtifact> CommitAsync(FirmwareArtifactMetadata metadata, CancellationToken cancellationToken = default);
}
