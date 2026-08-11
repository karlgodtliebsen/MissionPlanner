namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Base class for expected firmware subsystem failures.</summary>
public abstract class FirmwareException : Exception
{
    /// <summary>Initializes a firmware exception.</summary>
    protected FirmwareException(string message, Exception? innerException = null) : base(message, innerException) { }
}
