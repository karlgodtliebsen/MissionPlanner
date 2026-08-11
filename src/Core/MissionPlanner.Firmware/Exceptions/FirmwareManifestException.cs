namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates invalid or unavailable manifest data.</summary>
public sealed class FirmwareManifestException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
