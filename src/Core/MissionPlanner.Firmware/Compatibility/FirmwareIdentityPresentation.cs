using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Compatibility;

/// <summary>Formats independent identity observations without invented values or ambiguous board labels.</summary>
public static class FirmwareIdentityPresentation
{
    /// <summary>Formats source-labelled fields, omitting unavailable values.</summary>
    public static string Format(FirmwareIdentitySnapshot snapshot)
    {
        var lines = new List<string>();
        void Add(string label, object? value, string source)
        {
            if (value is not null && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                lines.Add($"  {label}: {value} [{source}]");
            }
        }
        lines.Add("Physical device");
        Add("MCU", snapshot.Physical?.McuFamily, snapshot.Physical?.Source.ToString() ?? "");
        Add("USB", snapshot.Physical?.Usb, "UsbDevice");
        Add("UID / serial", snapshot.Physical?.HardwareUid, "UsbDevice");
        lines.Add("Bootloader");
        Add("Target", snapshot.Bootloader?.Target, "BootloaderProtocol");
        Add("Bootloader board ID", snapshot.Bootloader?.BoardId, "BootloaderProtocol");
        Add("Revision", snapshot.Bootloader?.BootloaderRevision, "BootloaderProtocol");
        lines.Add("Running firmware");
        Add("Target", snapshot.Running?.Target, "StatusText");
        Add("Running firmware board ID", snapshot.Running?.BoardId, "AutopilotVersion");
        Add("Version", snapshot.Running?.Version, "AutopilotVersion");
        lines.Add("Selected firmware");
        var source = snapshot.Selected?.Source.ToString() ?? "";
        Add("Target", snapshot.Selected?.Target, source);
        Add("Board ID", snapshot.Selected?.BoardId, source);
        Add("Vehicle", snapshot.Selected?.VehicleType is FirmwareVehicleType.Unknown ? null : snapshot.Selected?.VehicleType, source);
        Add("Version", snapshot.Selected?.Version, source);
        return string.Join(Environment.NewLine, lines);
    }
}
