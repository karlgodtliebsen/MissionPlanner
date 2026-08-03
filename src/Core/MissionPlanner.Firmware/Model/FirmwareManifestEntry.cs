using System.Collections.ObjectModel;

namespace MissionPlanner.Firmware.Model;

/// <summary>Represents one normalized firmware catalogue release.</summary>
public sealed record FirmwareManifestEntry
{
    /// <summary>Initializes a manifest entry.</summary>
    public FirmwareManifestEntry(
        FirmwareVersion version,
        FirmwareReleaseChannel channel,
        FirmwareBoardTarget target,
        FirmwareArtifact artifact,
        string? gitSha = null,
        IReadOnlyDictionary<string, string>? rawMetadata = null)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        if (channel == FirmwareReleaseChannel.Custom)
        {
            throw new ArgumentException("Custom files are not catalogue releases.", nameof(channel));
        }

        if (gitSha is not null && (gitSha.Length is < 7 or > 64 || !gitSha.All(Uri.IsHexDigit)))
        {
            throw new ArgumentException("Git SHA must contain 7 to 64 hexadecimal characters.", nameof(gitSha));
        }

        Channel = channel;
        GitSha = gitSha?.ToLowerInvariant();
        RawMetadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(rawMetadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    /// <summary>Gets the firmware version.</summary>
    public FirmwareVersion Version { get; }

    /// <summary>Gets the release channel.</summary>
    public FirmwareReleaseChannel Channel { get; }

    /// <summary>Gets the hardware target.</summary>
    public FirmwareBoardTarget Target { get; }

    /// <summary>Gets the downloadable artifact.</summary>
    public FirmwareArtifact Artifact { get; }

    /// <summary>Gets the optional source revision.</summary>
    public string? GitSha { get; }

    /// <summary>Gets preserved source metadata for diagnostics.</summary>
    public IReadOnlyDictionary<string, string> RawMetadata { get; }
}
