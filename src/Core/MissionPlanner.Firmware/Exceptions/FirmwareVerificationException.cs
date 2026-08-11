namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates that programmed firmware could not be verified.</summary>
public sealed class FirmwareVerificationException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
