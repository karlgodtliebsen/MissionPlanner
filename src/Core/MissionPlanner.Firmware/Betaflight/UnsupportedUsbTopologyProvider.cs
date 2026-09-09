namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Fails closed on platforms without an audited physical USB topology adapter.</summary>
public sealed class UnsupportedUsbTopologyProvider : IUsbTopologyProvider
{
    /// <inheritdoc />
    public Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}