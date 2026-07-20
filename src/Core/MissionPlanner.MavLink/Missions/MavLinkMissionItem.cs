namespace MissionPlanner.MavLink.Missions;

/// <summary>
/// Provides the public API for MavLinkMissionItem.
/// </summary>
/// <param name="Sequence">The Sequence value.</param>
/// <param name="Frame">The Frame value.</param>
/// <param name="Command">The Command value.</param>
/// <param name="Current">The Current value.</param>
/// <param name="AutoContinue">The AutoContinue value.</param>
/// <param name="Param1">The Param1 value.</param>
/// <param name="Param2">The Param2 value.</param>
/// <param name="Param3">The Param3 value.</param>
/// <param name="Param4">The Param4 value.</param>
/// <param name="X">The X value.</param>
/// <param name="Y">The Y value.</param>
/// <param name="Z">The Z value.</param>
/// <param name="MissionType">The MissionType value.</param>
public sealed record MavLinkMissionItem(
    ushort Sequence,
    byte Frame,
    ushort Command,
    bool Current,
    bool AutoContinue,
    float Param1,
    float Param2,
    float Param3,
    float Param4,
    int X,
    int Y,
    float Z,
    MavMissionType MissionType);
