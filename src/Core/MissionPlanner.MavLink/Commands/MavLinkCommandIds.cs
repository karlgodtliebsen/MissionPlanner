namespace MissionPlanner.MavLink.Commands;

/// <summary>
/// Provides compatibility constants backed by the generated <see cref="Generated.MavCmd"/> enum.
/// </summary>
public static class MavLinkCommandIds
{
    /// <summary>Arms or disarms a vehicle component.</summary>
    public const ushort ComponentArmDisarm = (ushort)Generated.MavCmd.ComponentArmDisarm;

    /// <summary>Changes the vehicle mode.</summary>
    public const ushort DoSetMode = (ushort)Generated.MavCmd.DoSetMode;

    /// <summary>Requests a specific MAVLink message.</summary>
    public const ushort RequestMessage = (ushort)Generated.MavCmd.RequestMessage;

    /// <summary>Requests the current home position using the legacy command.</summary>
    public const ushort GetHomePosition = (ushort)Generated.MavCmd.GetHomePosition;

    /// <summary>Starts a vehicle takeoff.</summary>
    public const ushort NavTakeoff = (ushort)Generated.MavCmd.NavTakeoff;

    /// <summary>Starts execution of an uploaded mission.</summary>
    public const ushort MissionStart = (ushort)Generated.MavCmd.MissionStart;

    /// <summary>Sets the vehicle home position.</summary>
    public const ushort DoSetHome = (ushort)Generated.MavCmd.DoSetHome;

    /// <summary>Reboots or shuts down an autopilot component.</summary>
    public const ushort PreflightRebootShutdown = (ushort)Generated.MavCmd.PreflightRebootShutdown;
}
