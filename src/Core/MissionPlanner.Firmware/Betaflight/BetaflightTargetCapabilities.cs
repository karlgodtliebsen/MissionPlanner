namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Known BOARD_INFO capability bits; unknown bits remain in the underlying byte.</summary>
[Flags]
public enum BetaflightTargetCapabilities : byte
{
    /// <summary>No known capability reported.</summary>
    None = 0,
    /// <summary>USB virtual COM port.</summary>
    VirtualComPort = 1,
    /// <summary>Software serial support.</summary>
    SoftSerial = 2,
    /// <summary>Flash-resident bootloader; distinct from STM32 ROM DFU.</summary>
    FlashBootloader = 8,
    /// <summary>Receiver binding support.</summary>
    ReceiverBind = 64
}