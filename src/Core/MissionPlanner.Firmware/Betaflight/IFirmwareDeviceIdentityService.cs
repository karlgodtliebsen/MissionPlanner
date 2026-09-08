using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Enriches an existing discovery snapshot without enumerating another device list.</summary>
public interface IFirmwareDeviceIdentityService
{
    /// <summary>Adds bounded protocol identity while respecting active connection ownership.</summary>
    Task<IReadOnlyList<SerialDeviceDescriptor>> EnrichAsync(IReadOnlyList<SerialDeviceDescriptor> devices,
        bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>Invalidates evidence after programming or an operator request.</summary>
    void Invalidate();
}
