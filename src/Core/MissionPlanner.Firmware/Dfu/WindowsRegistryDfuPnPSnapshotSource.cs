using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Reads present USB DFU identities and driver evidence from the Windows Plug and Play registry.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryDfuPnPSnapshotSource : IWindowsDfuPnPSnapshotSource
{
    /// <inheritdoc />
    public Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var presentIds = PresentDeviceInstanceIds();
        var result = new List<WindowsDfuPnPSnapshot>();
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb is null) return Task.FromResult<IReadOnlyList<WindowsDfuPnPSnapshot>>(result);

        foreach (var hardwareName in usb.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryParseUsbIdentifier(hardwareName, out var vendorId, out var productId)) continue;
            using var hardware = usb.OpenSubKey(hardwareName);
            if (hardware is null) continue;
            foreach (var instanceName in hardware.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var instance = hardware.OpenSubKey(instanceName);
                if (instance is null) continue;
                var instanceId = $@"USB\{hardwareName}\{instanceName}";
                var driverKeyName = instance.GetValue("Driver") as string;
                using var driver = string.IsNullOrWhiteSpace(driverKeyName)
                    ? null
                    : Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Class\{driverKeyName}");
                result.Add(new WindowsDfuPnPSnapshot(
                    instanceId,
                    vendorId,
                    productId,
                    presentIds.Contains(instanceId),
                    FriendlyName: CleanText(instance.GetValue("FriendlyName") as string ?? instance.GetValue("DeviceDesc") as string),
                    Manufacturer: CleanText(instance.GetValue("Mfg") as string),
                    UsbSerialNumber: instanceName.Contains('&', StringComparison.Ordinal) ? null : instanceName,
                    DriverService: instance.GetValue("Service") as string,
                    DriverProvider: CleanText(driver?.GetValue("ProviderName") as string),
                    DriverVersion: driver?.GetValue("DriverVersion") as string,
                    ProblemCode: ReadInteger(instance.GetValue("Problem"))));
            }
        }

        return Task.FromResult<IReadOnlyList<WindowsDfuPnPSnapshot>>(result);
    }

    private static int? ReadInteger(object? value)
    {
        try { return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        catch (FormatException) { return null; }
        catch (InvalidCastException) { return null; }
        catch (OverflowException) { return null; }
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var separator = value.LastIndexOf(';');
        return (separator < 0 ? value : value[(separator + 1)..]).Trim();
    }

    private static bool TryParseUsbIdentifier(string value, out ushort vendorId, out ushort productId)
    {
        vendorId = 0;
        productId = 0;
        var parts = value.Split('&');
        return parts.Length >= 2 &&
               parts[0].StartsWith("VID_", StringComparison.OrdinalIgnoreCase) &&
               parts[1].StartsWith("PID_", StringComparison.OrdinalIgnoreCase) &&
               ushort.TryParse(parts[0].AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vendorId) &&
               ushort.TryParse(parts[1].AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out productId);
    }

    private static HashSet<string> PresentDeviceInstanceIds()
    {
        const uint presentDevices = 0x00000100;
        if (CM_Get_Device_ID_List_Size(out var characters, null, presentDevices) != 0 || characters == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new char[characters];
        if (CM_Get_Device_ID_List(null, buffer, characters, presentDevices) != 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_ID_List_SizeW", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List_Size(out uint characterCount, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_ID_ListW", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List(string? filter, [Out] char[] buffer, uint bufferLength, uint flags);
}
