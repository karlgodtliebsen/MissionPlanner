namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Base class for expected firmware subsystem failures.</summary>
public abstract class FirmwareException : Exception
{
    /// <summary>Initializes a firmware exception.</summary>
    protected FirmwareException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Indicates invalid or unavailable manifest data.</summary>
public sealed class FirmwareManifestException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates a firmware download failure.</summary>
public sealed class FirmwareDownloadException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates an invalid firmware package.</summary>
public sealed class FirmwarePackageException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates incompatible firmware and hardware.</summary>
public sealed class FirmwareCompatibilityException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates that a requested firmware device could not be found.</summary>
public sealed class FirmwareDeviceNotFoundException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates a bootloader communication or protocol failure.</summary>
public sealed class FirmwareBootloaderException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates that programmed firmware could not be verified.</summary>
public sealed class FirmwareVerificationException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates that another firmware operation already owns the subsystem.</summary>
public sealed class FirmwareBusyException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates that a normal vehicle connection conflicts with firmware serial ownership.</summary>
public sealed class FirmwareConnectionConflictException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
/// <summary>Indicates an illegal firmware-operation state transition.</summary>
public sealed class FirmwareStateTransitionException(string message, Exception? innerException = null) : FirmwareException(message, innerException);
