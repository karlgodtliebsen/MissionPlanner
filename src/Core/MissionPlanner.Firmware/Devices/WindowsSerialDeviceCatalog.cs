using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Enumerates Windows USB serial devices with stable Plug and Play identities.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSerialDeviceCatalog(TimeProvider timeProvider) : IFirmwareSerialDeviceCatalog
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var presentPorts = SerialPortNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var devices = new Dictionary<string, SerialDeviceDescriptor>(StringComparer.OrdinalIgnoreCase);
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb is not null)
        {
            foreach (var hardwareKey in usb.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryParseUsbIdentifier(hardwareKey, out var usbIdentifier)) continue;
                using var hardware = usb.OpenSubKey(hardwareKey);
                if (hardware is null) continue;
                foreach (var instanceKey in hardware.GetSubKeyNames())
                {
                    using var instance = hardware.OpenSubKey(instanceKey);
                    using var parameters = instance?.OpenSubKey("Device Parameters");
                    var portName = parameters?.GetValue("PortName") as string;
                    // Enum\USB retains records for devices that are no longer connected. Only
                    // enrich ports currently exposed by the serial subsystem; otherwise firmware
                    // discovery opens stale and unrelated historical COM ports.
                    if (string.IsNullOrWhiteSpace(portName) || !presentPorts.Contains(portName)) continue;
                    var product = CleanRegistryText(instance?.GetValue("FriendlyName") as string ?? instance?.GetValue("DeviceDesc") as string);
                    var manufacturer = CleanRegistryText(instance?.GetValue("Mfg") as string);
                    var stableId = $@"USB\{hardwareKey}\{instanceKey}";
                    devices[stableId] = new SerialDeviceDescriptor(
                        portName,
                        stableId,
                        usbIdentifier,
                        instanceKey.Contains('&', StringComparison.Ordinal) ? null : instanceKey,
                        product,
                        manufacturer,
                        product is null ? [] : [product],
                        timeProvider.GetUtcNow());
                }
            }
        }

        foreach (var port in presentPorts)
        {
            if (devices.Values.All(device => !string.Equals(device.PortName, port, StringComparison.OrdinalIgnoreCase)))
                devices[$"transient:{port}"] = new SerialDeviceDescriptor(port, arrivedAt: timeProvider.GetUtcNow());
        }
        return Task.FromResult<IReadOnlyList<SerialDeviceDescriptor>>(devices.Values.OrderBy(device => device.PortName, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IEnumerable<string> SerialPortNames() => System.IO.Ports.SerialPort.GetPortNames().Distinct(StringComparer.OrdinalIgnoreCase);
    private static string? CleanRegistryText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var separator = value.LastIndexOf(';');
        return (separator >= 0 ? value[(separator + 1)..] : value).Trim();
    }
    private static bool TryParseUsbIdentifier(string key, out UsbIdentifier identifier)
    {
        identifier = default;
        var parts = key.Split('&');
        if (parts.Length < 2 || !parts[0].StartsWith("VID_", StringComparison.OrdinalIgnoreCase) || !parts[1].StartsWith("PID_", StringComparison.OrdinalIgnoreCase)) return false;
        if (!int.TryParse(parts[0].AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vendor) ||
            !int.TryParse(parts[1].AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var product) || vendor <= 0 || product <= 0) return false;
        identifier = new UsbIdentifier(vendor, product);
        return true;
    }
}
