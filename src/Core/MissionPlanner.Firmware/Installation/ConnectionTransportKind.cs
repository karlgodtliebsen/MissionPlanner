namespace MissionPlanner.Firmware.Installation;

/// <summary>Identifies the active normal connection transport.</summary>
public enum ConnectionTransportKind
{
    /// <summary>A serial or USB connection.</summary>
    Serial,

    /// <summary>A TCP connection.</summary>
    Tcp,

    /// <summary>A UDP connection.</summary>
    Udp,

    /// <summary>Another connection kind.</summary>
    Other
}
