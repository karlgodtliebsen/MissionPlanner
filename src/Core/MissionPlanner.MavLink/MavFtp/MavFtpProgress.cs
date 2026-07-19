namespace MissionPlanner.MavLink.MavFtp;

public sealed record MavFtpProgress(string RemotePath, long BytesTransferred, long? TotalBytes, double? BytesPerSecond);
