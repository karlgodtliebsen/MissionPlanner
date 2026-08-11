namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates that a requested firmware device could not be found.</summary>
public sealed class FirmwareDeviceNotFoundException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
