namespace MissionPlanner.MavLink.MavFtp;

public enum MavFtpOpcode : byte
{
    None = 0, TerminateSession = 1, ResetSessions = 2, ListDirectory = 3,
    OpenFileReadOnly = 4, ReadFile = 5, CreateFile = 6, WriteFile = 7,
    RemoveFile = 8, CreateDirectory = 9, RemoveDirectory = 10, OpenFileWriteOnly = 11,
    TruncateFile = 12, Rename = 13, CalculateFileCrc32 = 14, BurstReadFile = 15,
    ListDirectoryWithTime = 16, Ack = 128, Nak = 129
}
