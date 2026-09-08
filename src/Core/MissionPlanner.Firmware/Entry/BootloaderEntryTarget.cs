namespace MissionPlanner.Firmware.Entry;

/// <summary>Separates serial ArduPilot bootloaders from STM32 factory USB DFU.</summary>
public enum BootloaderEntryTarget
{
    /// <summary>Existing serial application-install path.</summary>
    ArduPilotSerial,
    /// <summary>Factory STM32 USB DFU entry, requiring subsequent physical correlation.</summary>
    Stm32RomDfu
}
