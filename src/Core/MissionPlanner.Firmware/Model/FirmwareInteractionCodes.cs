namespace MissionPlanner.Firmware.Model;

/// <summary>Defines stable codes for firmware interactions presented by a host.</summary>
public static class FirmwareInteractionCodes
{
    /// <summary>Requests that the operator reconnect or reset the controller to enter its bootloader.</summary>
    public const string ManualBootloaderReconnect = "firmware.manual-bootloader-reconnect";

    /// <summary>Requests that the operator wait for the application firmware after a reboot.</summary>
    public const string ReconnectAfterReboot = "firmware.reconnect-after-reboot";
}
