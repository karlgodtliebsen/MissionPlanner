namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates a bootloader communication or protocol failure.</summary>
public sealed class FirmwareBootloaderException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
