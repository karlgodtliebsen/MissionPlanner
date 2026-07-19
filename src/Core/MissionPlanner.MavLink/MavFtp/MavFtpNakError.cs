namespace MissionPlanner.MavLink.MavFtp;

public enum MavFtpNakError : byte
{
    None = 0, Failure = 1, FailureErrno = 2, InvalidDataSize = 3,
    InvalidSession = 4, NoSessionsAvailable = 5, EndOfFile = 6,
    UnknownCommand = 7, FileExists = 8, FileProtected = 9, FileNotFound = 10
}
