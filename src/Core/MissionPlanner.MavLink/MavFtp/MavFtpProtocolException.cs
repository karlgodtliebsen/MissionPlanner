namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpProtocolException.
/// </summary>
public sealed class MavFtpProtocolException(string message, Exception? innerException = null) : Exception(message, innerException);
