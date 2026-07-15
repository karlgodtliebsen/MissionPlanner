namespace MissionPlanner.MavLink.Missions;

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
