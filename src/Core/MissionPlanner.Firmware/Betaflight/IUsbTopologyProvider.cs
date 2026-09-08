namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Returns comparable physical USB location evidence from platform device identities.</summary>
public interface IUsbTopologyProvider
{
    /// <summary>Gets the physical port location or null when the platform cannot prove it.</summary>
    Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default);
}

/// <summary>Fails closed on platforms without an audited physical USB topology adapter.</summary>
public sealed class UnsupportedUsbTopologyProvider : IUsbTopologyProvider
{
    /// <inheritdoc />
    public Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
