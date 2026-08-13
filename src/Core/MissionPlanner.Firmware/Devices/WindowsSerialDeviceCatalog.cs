using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
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
        Debug.Print("Windows GetDevicesAsync ");

        cancellationToken.ThrowIfCancellationRequested();
        var presentPorts = SerialPortNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var presentDeviceIds = PresentDeviceInstanceIds();
        var knownUsbPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var devices = new Dictionary<string, SerialDeviceDescriptor>(StringComparer.OrdinalIgnoreCase);
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb is not null)
        {
            Debug.Print("Windows GetDevicesAsync usb");

            foreach (var hardwareKey in usb.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryParseUsbIdentifier(hardwareKey, out var usbIdentifier))
                {
                    continue;
                }

                using var hardware = usb.OpenSubKey(hardwareKey);
                if (hardware is null)
                {
                    continue;
                }

                foreach (var instanceKey in hardware.GetSubKeyNames())
                {
                    using var instance = hardware.OpenSubKey(instanceKey);
                    using var parameters = instance?.OpenSubKey("Device Parameters");
                    var portName = parameters?.GetValue("PortName") as string;
                    if (string.IsNullOrWhiteSpace(portName))
                    {
                        continue;
                    }

                    knownUsbPorts.Add(portName);
                    var stableId = $@"USB\{hardwareKey}\{instanceKey}";
                    // Both Enum\USB and SerialPort.GetPortNames can retain disconnected device
                    // history. Configuration Manager supplies the authoritative set of currently
                    // present PnP instances; require that identity and current serial exposure.
                    if (!presentDeviceIds.Contains(stableId) || !presentPorts.Contains(portName))
                    {
                        continue;
                    }

                    var product = CleanRegistryText(instance?.GetValue("FriendlyName") as string ?? instance?.GetValue("DeviceDesc") as string);
                    var manufacturer = CleanRegistryText(instance?.GetValue("Mfg") as string);
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
            Debug.Print("Windows GetDevicesAsync port {0}", port);

            // A known USB port without an active PnP instance is stale history, not a transient
            // device. Unknown ports are retained for non-USB and very short-lived bootloaders.
            if (knownUsbPorts.Contains(port))
            {
                continue;
            }

            if (devices.Values.All(device => !string.Equals(device.PortName, port, StringComparison.OrdinalIgnoreCase)))
            {
                devices[$"transient:{port}"] = new SerialDeviceDescriptor(port, arrivedAt: timeProvider.GetUtcNow());
            }
        }

        return Task.FromResult<IReadOnlyList<SerialDeviceDescriptor>>(devices.Values.OrderBy(device => device.PortName, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static HashSet<string> PresentDeviceInstanceIds()
    {
        const uint presentDevices = 0x00000100;
        if (CM_Get_Device_ID_List_Size(out var characterCount, null, presentDevices) != 0 || characterCount == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var buffer = new char[characterCount];
        return CM_Get_Device_ID_List(null, buffer, characterCount, presentDevices) != 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new string(buffer)
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SerialPortNames()
    {
        return System.IO.Ports.SerialPort.GetPortNames().Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? CleanRegistryText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var separator = value.LastIndexOf(';');
        return (separator >= 0 ? value[(separator + 1)..] : value).Trim();
    }

    private static bool TryParseUsbIdentifier(string key, out UsbIdentifier identifier)
    {
        identifier = default;
        var parts = key.Split('&');
        if (parts.Length < 2 || !parts[0].StartsWith("VID_", StringComparison.OrdinalIgnoreCase) || !parts[1].StartsWith("PID_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[0].AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vendor) ||
            !int.TryParse(parts[1].AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var product) || vendor <= 0 || product <= 0)
        {
            return false;
        }

        identifier = new UsbIdentifier(vendor, product);
        return true;
    }

    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_ID_List_SizeW", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List_Size(out uint characterCount, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_ID_ListW", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List(string? filter, [Out] char[] buffer, uint bufferLength, uint flags);
}
