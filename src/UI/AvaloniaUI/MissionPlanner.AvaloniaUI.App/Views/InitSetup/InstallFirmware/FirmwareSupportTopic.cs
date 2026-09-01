namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Identifies an offline firmware-help topic.</summary>
public enum FirmwareSupportTopic
{
    /// <summary>Exact hardware target selection.</summary>
    ChoosingFirmware,

    /// <summary>Release stability and risk.</summary>
    ReleaseChannels,

    /// <summary>Supported package and programming file formats.</summary>
    FileTypes,

    /// <summary>Serial installation compared with USB DFU.</summary>
    InstallationModes,

    /// <summary>Board-specific bootloader entry.</summary>
    EnteringBootMode,

    /// <summary>Windows enumeration evidence.</summary>
    WindowsDevices,

    /// <summary>Driver and programming-tool guidance.</summary>
    DriverTools,

    /// <summary>Host-platform feature boundaries.</summary>
    PlatformLimitations,

    /// <summary>Safe recovery and evidence collection.</summary>
    Recovery
}

