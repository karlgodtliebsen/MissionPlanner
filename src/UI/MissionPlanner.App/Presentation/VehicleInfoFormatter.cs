using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Presentation;

/// <summary>Formats user annotations separately from reported vehicle identity.</summary>
public static class VehicleInfoFormatter
{
    /// <summary>Builds a copyable, read-only description of the inspected vehicle.</summary>
    public static string Format(VehicleState? state)
    {
        if (state is null)
        {
            return "No vehicle information is available. Connect a vehicle to view its details.";
        }
        var details = state.LocalDetails;
        var firmware = state.Identity.Firmware;
        return $"Local vehicle details\n\n" +
            $"Nickname: {Value(details?.Nickname)}\n" +
            $"Product: {Value(details?.ProductName)}\n" +
            $"Product information URL: {Value(details?.ProductUrl)}\n\n" +
            "Edit these locally saved descriptions in Optional Hardware → Naming.\n\n" +
            "Reported vehicle identity\n\n" +
            $"Vehicle: {state.DisplayName}\n" +
            $"MAVLink system / component: {state.VehicleId}\n" +
            $"Firmware: {VehicleFirmwareDisplayFormatter.Format(firmware)}\n" +
            $"Vehicle type: {firmware.MavType}\n" +
            $"Autopilot type: {firmware.Autopilot}\n" +
            $"Hardware UID: {firmware.HardwareUid?.ToString("X16") ?? "Not reported"}\n" +
            $"Extended hardware UID: {Value(firmware.HardwareUid2, "Not reported")}\n" +
            $"Connection: {state.Connection.State}";
    }

    private static string Value(string? value, string fallback = "Not set") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
