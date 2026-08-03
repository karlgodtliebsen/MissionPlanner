namespace MissionPlanner.Firmware;

/// <summary>
/// Configures the firmware subsystem. Concrete catalogue, cache, discovery, and protocol
/// settings are added alongside their implementations so they can be validated at startup.
/// </summary>
public sealed class FirmwareOptions
{
    /// <summary>Gets or sets the official manifest URI.</summary>
    public Uri ManifestUri { get; set; } = new("https://firmware.ardupilot.org/manifest.json.gz");

    /// <summary>Gets or sets how long a cached manifest is considered fresh.</summary>
    public TimeSpan CatalogCacheDuration { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Gets or sets the maximum accepted decompressed manifest size.</summary>
    public int MaximumManifestBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Gets or sets the maximum accepted decompressed firmware image size.</summary>
    public int MaximumFirmwareImageBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Gets or sets the ordinary bootloader command timeout.</summary>
    public TimeSpan BootloaderCommandTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the longer chip erase timeout.</summary>
    public TimeSpan BootloaderEraseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets synchronization retry count.</summary>
    public int BootloaderSyncAttempts { get; set; } = 3;

    /// <summary>Gets or sets the named settle delay between synchronization attempts.</summary>
    public TimeSpan BootloaderRetryDelay { get; set; } = TimeSpan.FromMilliseconds(40);
}
