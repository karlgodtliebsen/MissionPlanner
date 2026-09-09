using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Owns a safe reboot and same-physical-device handoff into the existing DFU stack.</summary>
public interface IBetaflightDfuHandoff
{
    /// <summary>Reboots the explicitly selected controller and proves its returning DFU endpoint.</summary>
    Task<BetaflightDfuHandoffResult> RebootAsync(SerialDeviceDescriptor source, IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}