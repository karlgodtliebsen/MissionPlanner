namespace MissionPlanner.Firmware.Configuration;

/// <summary>
/// Configures the firmware subsystem. Concrete catalogue, cache, discovery, and protocol
/// settings are added alongside their implementations so they can be validated at startup.
/// </summary>
public sealed class FirmwareOptions
{
    /// <summary>
    /// Gets or sets the name of the configuration section.
    /// </summary>
    public static string SectionName { get; set; } = "Firmware";

    /// <summary>Gets or sets the official manifest URI.</summary>
    public Uri ManifestUri { get; set; } = new("https://firmware.ardupilot.org/manifest.json.gz");

    /// <summary>Gets or sets how long a cached manifest is considered fresh.</summary>
    public TimeSpan CatalogCacheDuration { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Gets or sets the maximum accepted decompressed manifest size.</summary>
    public int MaximumManifestBytes { get; set; } = 128 * 1024 * 1024;

    /// <summary>Gets or sets the maximum compressed or plain manifest response downloaded over HTTP.</summary>
    public int MaximumManifestDownloadBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Gets or sets the bounded timeout for firmware HTTP requests.</summary>
    public TimeSpan HttpRequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the product token sent with firmware HTTP requests.</summary>
    public string HttpUserAgent { get; set; } = "MissionPlanner/1.0";

    /// <summary>Gets or sets the maximum accepted decompressed firmware image size.</summary>
    public int MaximumFirmwareImageBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Gets or sets the ordinary bootloader command timeout.</summary>
    public TimeSpan BootloaderCommandTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the short timeout used while synchronizing a bootloader candidate.</summary>
    public TimeSpan BootloaderSynchronizationTimeout { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Gets or sets the longer chip erase timeout.</summary>
    public TimeSpan BootloaderEraseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets synchronization retry count.</summary>
    public int BootloaderSyncAttempts { get; set; } = 3;

    /// <summary>Gets or sets the named settle delay between synchronization attempts.</summary>
    public TimeSpan BootloaderRetryDelay { get; set; } = TimeSpan.FromMilliseconds(40);

    /// <summary>Gets or sets the overall bootloader discovery timeout.</summary>
    public TimeSpan BootloaderDiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how often unchanged serial ports are re-probed during discovery.</summary>
    public TimeSpan BootloaderDiscoveryPollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets the maximum time allowed to open one candidate port.</summary>
    public TimeSpan BootloaderPortOpenTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the baud rate used by modern bootloaders.</summary>
    public int BootloaderBaudRate { get; set; } = 115200;

    /// <summary>Gets or sets the bounded wait for a heartbeat on a temporary application channel.</summary>
    public TimeSpan TemporaryMavLinkHeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the bounded wait for reboot acknowledgement before discovery takes over.</summary>
    public TimeSpan TemporaryMavLinkCommandAckTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the maximum encoded artifact download size.</summary>
    public long MaximumArtifactBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>Gets or sets whether non-HTTPS artifact URLs are allowed for controlled development.</summary>
    public bool AllowInsecureArtifactUrls { get; set; }
}
