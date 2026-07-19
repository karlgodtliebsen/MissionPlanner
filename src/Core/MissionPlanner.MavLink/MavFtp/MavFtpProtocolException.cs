namespace MissionPlanner.MavLink.MavFtp;

public sealed class MavFtpProtocolException(string message, Exception? innerException = null) : Exception(message, innerException);
