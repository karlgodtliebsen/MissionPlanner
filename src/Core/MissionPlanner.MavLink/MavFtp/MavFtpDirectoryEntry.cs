namespace MissionPlanner.MavLink.MavFtp;

public sealed record MavFtpDirectoryEntry(string Name, MavFtpDirectoryEntryType Type, long? Size);
