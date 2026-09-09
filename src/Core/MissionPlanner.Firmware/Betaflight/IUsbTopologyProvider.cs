namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Returns comparable physical USB location evidence from platform device identities.</summary>
public interface IUsbTopologyProvider
{
    /// <summary>Gets the physical port location or null when the platform cannot prove it.</summary>
    Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default);
}