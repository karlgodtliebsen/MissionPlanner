namespace MissionPlanner.Firmware.Devices;

/// <summary>Identifies the kind of serial device change.</summary>
public enum FirmwareDeviceChangeKind
{
    /// <summary>A device became available.</summary>
    Arrived,

    /// <summary>A device stopped being available.</summary>
    Removed
}
