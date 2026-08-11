namespace MissionPlanner.Firmware.Downloads;

/// <summary>Represents an atomically committed artifact.</summary>
public interface IFirmwareStoredArtifact
{
    /// <summary>Gets immutable artifact metadata.</summary>
    FirmwareArtifactMetadata Metadata { get; }

    /// <summary>Gets the provider-readable local path when storage is file-backed.</summary>
    string? LocalPath => null;

    /// <summary>Opens a new read stream.</summary>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
