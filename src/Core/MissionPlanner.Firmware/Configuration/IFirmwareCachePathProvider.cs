namespace MissionPlanner.Firmware.Configuration;

/// <summary>Provides the host-selected durable firmware cache root.</summary>
public interface IFirmwareCachePathProvider
{
    /// <summary>Gets the absolute firmware cache root.</summary>
    string CacheRoot { get; }
}
