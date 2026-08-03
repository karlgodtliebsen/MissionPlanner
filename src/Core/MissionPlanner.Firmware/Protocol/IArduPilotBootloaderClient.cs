using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Protocol;

/// <summary>Executes the modern PX4/ArduPilot serial bootloader protocol.</summary>
public interface IArduPilotBootloaderClient : IAsyncDisposable
{
    /// <summary>Synchronizes and reads board identity before erase.</summary>
    Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default);
    /// <summary>Erases application flash.</summary>
    Task EraseAsync(CancellationToken cancellationToken = default);
    /// <summary>Programs internal and optional external application images.</summary>
    Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default);
    /// <summary>Verifies programmed checksums.</summary>
    Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default);
    /// <summary>Requests application reboot.</summary>
    Task RebootAsync(CancellationToken cancellationToken = default);
}
