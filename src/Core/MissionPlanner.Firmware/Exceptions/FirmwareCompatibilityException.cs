namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates incompatible firmware and hardware.</summary>
public sealed class FirmwareCompatibilityException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
