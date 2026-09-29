using System.Globalization;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Core.Vehicles;

/// <summary>User-provided local annotations, never flight-controller parameters or firmware evidence.</summary>
public sealed record VehicleLocalDetails(string ProductName, string ProductUrl, string Nickname)
{
    /// <summary>Gets a hardware-based storage key; transient addresses are deliberately excluded.</summary>
    public static string? GetKey(VehicleFirmwareIdentity firmware) =>
        !string.IsNullOrWhiteSpace(firmware.HardwareUid2) && firmware.HardwareUid2.Any(c => c != '0')
            ? "uid2:" + firmware.HardwareUid2.ToLowerInvariant()
            : firmware.HardwareUid is > 0
                ? "uid:" + firmware.HardwareUid.Value.ToString("X16", CultureInfo.InvariantCulture)
                : null;
}
