using System.Globalization;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions;

/// <summary>Describes one command selectable in the mission editor.</summary>
/// <param name="Name">The v1.38 mavcmd.xml style display name.</param>
/// <param name="Id">The numeric MAV_CMD identifier.</param>
/// <param name="Command">The domain command the identifier maps to.</param>
public sealed record MissionCommandDescriptor(string Name, ushort Id, MissionCommand Command);

/// <summary>Describes one altitude frame selectable in the mission editor.</summary>
/// <param name="Name">The v1.38 altmode style display name.</param>
/// <param name="Id">The numeric MAV_FRAME identifier.</param>
/// <param name="Frame">The domain frame the identifier maps to.</param>
public sealed record MissionFrameDescriptor(string Name, byte Id, MissionFrame Frame);

/// <summary>
/// Single owner of the command and frame names and numeric identifiers offered by the mission
/// editor. The set is limited to the commands the mission domain edits directly.
/// </summary>
public static class MissionCommandCatalog
{
    /// <summary>Gets the commands selectable in the waypoint editor, in display order.</summary>
    public static IReadOnlyList<MissionCommandDescriptor> Commands { get; } =
    [
        new("WAYPOINT", (ushort)MissionCommand.Waypoint, MissionCommand.Waypoint),
        new("LOITER_UNLIM", (ushort)MissionCommand.LoiterUnlimited, MissionCommand.LoiterUnlimited),
        new("LOITER_TURNS", (ushort)MissionCommand.LoiterTurns, MissionCommand.LoiterTurns),
        new("LOITER_TIME", (ushort)MissionCommand.LoiterTime, MissionCommand.LoiterTime),
        new("RETURN_TO_LAUNCH", (ushort)MissionCommand.ReturnToLaunch, MissionCommand.ReturnToLaunch),
        new("LAND", (ushort)MissionCommand.Land, MissionCommand.Land),
        new("TAKEOFF", (ushort)MissionCommand.Takeoff, MissionCommand.Takeoff),
        new("SPLINE_WAYPOINT", (ushort)MissionCommand.SplineWaypoint, MissionCommand.SplineWaypoint),
        new("DO_JUMP", (ushort)MissionCommand.Jump, MissionCommand.Jump),
        new("DO_CHANGE_SPEED", (ushort)MissionCommand.ChangeSpeed, MissionCommand.ChangeSpeed),
        new("DO_SET_ROI_LOCATION", (ushort)MissionCommand.SetRoiLocation, MissionCommand.SetRoiLocation)
    ];

    /// <summary>Gets the altitude frames selectable in the waypoint editor, in display order.</summary>
    public static IReadOnlyList<MissionFrameDescriptor> Frames { get; } =
    [
        new("Absolute", (byte)MissionFrame.Global, MissionFrame.Global),
        new("Mission", (byte)MissionFrame.Mission, MissionFrame.Mission),
        new("Relative", (byte)MissionFrame.GlobalRelativeAltitude, MissionFrame.GlobalRelativeAltitude),
        new("Terrain", (byte)MissionFrame.GlobalTerrainAltitude, MissionFrame.GlobalTerrainAltitude)
    ];

    /// <summary>Gets the command names in display order.</summary>
    public static IReadOnlyList<string> CommandNames { get; } = Commands.Select(command => command.Name).ToArray();

    /// <summary>Gets the frame names in display order.</summary>
    public static IReadOnlyList<string> FrameNames { get; } = Frames.Select(frame => frame.Name).ToArray();

    /// <summary>Gets the display name for a command identifier, or <c>ID n</c> when it is not editable.</summary>
    public static string CommandNameFor(ushort commandId)
    {
        foreach (var command in Commands)
        {
            if (command.Id == commandId)
            {
                return command.Name;
            }
        }

        return $"ID {commandId}";
    }

    /// <summary>Gets the identifier for a command display name, or <see langword="null"/> when unknown.</summary>
    public static ushort? CommandIdFor(string? commandName)
    {
        foreach (var command in Commands)
        {
            if (command.Name == commandName)
            {
                return command.Id;
            }
        }

        return null;
    }

    /// <summary>Gets the display name for a frame identifier, or its number when it is not editable.</summary>
    public static string FrameNameFor(byte frameId)
    {
        foreach (var frame in Frames)
        {
            if (frame.Id == frameId)
            {
                return frame.Name;
            }
        }

        return frameId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Gets the identifier for a frame display name, or <see langword="null"/> when unknown.</summary>
    public static byte? FrameIdFor(string? frameName)
    {
        foreach (var frame in Frames)
        {
            if (frame.Name == frameName)
            {
                return frame.Id;
            }
        }

        return null;
    }
}
