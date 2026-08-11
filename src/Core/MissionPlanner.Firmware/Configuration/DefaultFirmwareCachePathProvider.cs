namespace MissionPlanner.Firmware.Configuration;

/// <summary>Provides a desktop-safe default cache location without UI framework dependencies.</summary>
public sealed class DefaultFirmwareCachePathProvider : IFirmwareCachePathProvider
{
    /// <inheritdoc />
    public string CacheRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MissionPlanner", "Firmware");
}
