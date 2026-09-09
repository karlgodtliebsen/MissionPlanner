namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Installation mechanism selected in the presentation layer.</summary>
public enum FirmwareSection
{
    /// <summary>Normal ArduPilot serial bootloader workflow.</summary>
    Firmware,
    /// <summary>STM32 ROM USB DFU workflow.</summary>
    Stm32Dfu,
    /// <summary>Offline help and support.</summary>
    Help
}

/// <summary>Source or device context within STM32 DFU.</summary>
public enum Stm32DfuSection
{
    /// <summary>Device identification and safe DFU entry.</summary>
    Device,
    /// <summary>Official combined HEX from a catalogue release.</summary>
    Catalogue,
    /// <summary>Local combined HEX and explicit platform.</summary>
    Custom
}
