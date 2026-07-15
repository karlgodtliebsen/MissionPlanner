namespace MissionPlanner.MavLink.Missions;

public enum MavMissionResult : byte
{
    Accepted = 0,
    Error = 1,
    UnsupportedFrame = 2,
    Unsupported = 3,
    NoSpace = 4,
    Invalid = 5,
    InvalidParam1 = 6,
    InvalidParam2 = 7,
    InvalidParam3 = 8,
    InvalidParam4 = 9,
    InvalidParam5X = 10,
    InvalidParam6Y = 11,
    InvalidParam7 = 12,
    InvalidSequence = 13,
    Denied = 14,
    OperationCancelled = 15
}
