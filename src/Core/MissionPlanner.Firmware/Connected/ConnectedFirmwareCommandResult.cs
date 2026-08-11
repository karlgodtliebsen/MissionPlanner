namespace MissionPlanner.Firmware.Connected;

/// <summary>Identifies the precise connected command outcome.</summary>
public enum ConnectedFirmwareCommandResult
{
    /// <summary>The vehicle accepted the command.</summary>
    Accepted,

    /// <summary>The vehicle temporarily rejected the command.</summary>
    TemporarilyRejected,

    /// <summary>The vehicle denied the command.</summary>
    Denied,

    /// <summary>The vehicle does not support the command or embedded image.</summary>
    Unsupported,

    /// <summary>The command failed.</summary>
    Failed,

    /// <summary>No terminal acknowledgement arrived before timeout.</summary>
    Timeout
}
