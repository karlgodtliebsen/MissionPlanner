namespace MissionPlanner.Firmware.Configuration;

/// <summary>Provides the host-selected durable firmware cache root.</summary>
public interface IFirmwareCachePathProvider
{
    /// <summary>Gets the absolute firmware cache root.</summary>
    string CacheRoot { get; }
}

/// <summary>Provides a desktop-safe default cache location without UI framework dependencies.</summary>
public sealed class DefaultFirmwareCachePathProvider : IFirmwareCachePathProvider
{
    /// <inheritdoc />
    public string CacheRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MissionPlanner", "Firmware");
}
