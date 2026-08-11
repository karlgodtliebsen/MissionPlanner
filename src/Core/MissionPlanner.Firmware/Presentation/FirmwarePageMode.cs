namespace MissionPlanner.Firmware.Presentation;

/// <summary>Identifies the presentation mode of the firmware page.</summary>
public enum FirmwarePageMode
{
    /// <summary>A supported ArduPilot vehicle is connected.</summary>
    Connected,

    /// <summary>No vehicle is connected and application firmware may be installed.</summary>
    Disconnected,

    /// <summary>A firmware operation owns the global operation boundary.</summary>
    OperationInProgress,

    /// <summary>Direct firmware installation is unavailable on the current platform.</summary>
    UnsupportedPlatform
}
