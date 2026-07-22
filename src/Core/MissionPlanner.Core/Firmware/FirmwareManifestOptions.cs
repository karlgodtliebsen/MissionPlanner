namespace MissionPlanner.Core.Firmware;

/// <summary>Configures firmware manifest discovery.</summary>
public sealed class FirmwareManifestOptions
{
    /// <summary>The application configuration section.</summary>
    public const string SectionName = "Firmware";

    /// <summary>Gets or sets the optional HTTPS JSON manifest URI.</summary>
    public string? ManifestUrl { get; set; }

    /// <summary>Gets or sets statically configured releases used when no remote manifest is configured.</summary>
    public List<FirmwareManifestEntry> Releases { get; set; } = [];
}
