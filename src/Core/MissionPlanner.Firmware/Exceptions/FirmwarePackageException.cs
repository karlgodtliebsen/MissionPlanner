namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates an invalid firmware package.</summary>
public sealed class FirmwarePackageException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
