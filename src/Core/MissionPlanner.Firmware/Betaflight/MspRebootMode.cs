namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Semantic reboot modes defined by Betaflight's MSP implementation.</summary>
public enum MspRebootMode : byte
{
    /// <summary>Restart the currently installed firmware.</summary>
    Firmware = 0,
    /// <summary>Enter the MCU factory ROM bootloader.</summary>
    RomBootloader = 1
}