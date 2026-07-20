namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpFileInfo.
/// </summary>
public sealed record MavFtpFileInfo(string RemotePath, long Size);
