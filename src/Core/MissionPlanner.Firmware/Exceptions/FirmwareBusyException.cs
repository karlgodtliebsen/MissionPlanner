namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates that another firmware operation already owns the subsystem.</summary>
public sealed class FirmwareBusyException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
