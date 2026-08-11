namespace MissionPlanner.Firmware.Connected;

/// <summary>Provides the existing host command/ACK route to a connected ArduPilot vehicle.</summary>
public interface IConnectedVehicleFirmwareGateway
{
    /// <summary>Gets whether a vehicle is connected.</summary>
    bool IsConnected { get; }

    /// <summary>Gets whether the connected vehicle is armed.</summary>
    bool IsArmed { get; }

    /// <summary>Gets whether the connected autopilot is an ArduPilot family.</summary>
    bool IsSupportedArduPilot { get; }

    /// <summary>
    /// Sends MAV_CMD_FLASH_BOOTLOADER with parameter 5 set to the official ArduPilot confirmation
    /// value 290876 through the host command service and ACK tracker.
    /// </summary>
    Task<ConnectedFirmwareCommandResult> FlashEmbeddedBootloaderAsync(CancellationToken cancellationToken = default);
}
