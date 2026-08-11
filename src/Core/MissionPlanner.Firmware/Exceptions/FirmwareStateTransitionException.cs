namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates an illegal firmware-operation state transition.</summary>
public sealed class FirmwareStateTransitionException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
