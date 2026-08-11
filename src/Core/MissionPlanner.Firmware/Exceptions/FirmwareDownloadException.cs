namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates a firmware download failure.</summary>
public sealed class FirmwareDownloadException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
