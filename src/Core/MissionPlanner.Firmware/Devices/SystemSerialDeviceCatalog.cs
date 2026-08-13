using System.Diagnostics;
using System.IO.Ports;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Provides a cross-platform serial snapshot without probing or opening devices.</summary>
public sealed class SystemSerialDeviceCatalog(TimeProvider timeProvider) : IFirmwareSerialDeviceCatalog
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Print("System GetDevicesAsync");

        IReadOnlyList<SerialDeviceDescriptor> result = SerialPort.GetPortNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new SerialDeviceDescriptor(name, arrivedAt: timeProvider.GetUtcNow()))
            .ToArray();

        Debug.Print("System GetDevicesAsync completed {0}", result.Count);

        return Task.FromResult(result);
    }
}
