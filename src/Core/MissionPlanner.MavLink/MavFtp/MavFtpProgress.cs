namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpProgress.
/// </summary>
public sealed record MavFtpProgress(string RemotePath, long BytesTransferred, long? TotalBytes, double? BytesPerSecond);
