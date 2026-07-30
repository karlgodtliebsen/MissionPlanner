namespace MissionPlanner.Core.Simulation;

/// <summary>Configures verified SITL manifests, external installations, and extraction limits.</summary>
public sealed class SitlManifestOptions
{
    /// <summary>Application configuration section.</summary>
    public const string SectionName = "Sitl";

    /// <summary>Gets or sets the optional official HTTPS manifest URL.</summary>
    public string? ManifestUrl { get; set; }

    /// <summary>Gets or sets statically configured verified releases.</summary>
    public List<SitlManifestEntry> Releases { get; set; } = [];

    /// <summary>Gets or sets user-configured external installations.</summary>
    public List<ExternalSitlInstallationOptions> ExternalInstallations { get; set; } = [];

    /// <summary>Gets or sets the maximum accepted archive size.</summary>
    public long MaximumArchiveBytes { get; set; } = 1024L * 1024 * 1024;

    /// <summary>Gets or sets the maximum total extracted size.</summary>
    public long MaximumExtractedBytes { get; set; } = 4L * 1024 * 1024 * 1024;
}
