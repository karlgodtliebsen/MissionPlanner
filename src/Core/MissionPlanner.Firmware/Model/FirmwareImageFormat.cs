namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies a firmware package format.</summary>
public enum FirmwareImageFormat
{
    /// <summary>An ArduPilot JSON package.</summary>
    Apj,

    /// <summary>A PX4 JSON package.</summary>
    Px4,

    /// <summary>An Intel HEX image.</summary>
    IntelHex,

    /// <summary>An ArduPilot binary container.</summary>
    Abin,

    /// <summary>An unrecognized format.</summary>
    Unknown
}
