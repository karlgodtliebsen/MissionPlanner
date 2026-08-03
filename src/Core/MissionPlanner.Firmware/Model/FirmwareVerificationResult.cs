namespace MissionPlanner.Firmware.Model;

/// <summary>Contains bootloader checksum verification evidence.</summary>
public sealed record FirmwareVerificationResult(
    bool Succeeded,
    uint ExpectedChecksum,
    uint ActualChecksum,
    uint? ExpectedExternalChecksum = null,
    uint? ActualExternalChecksum = null);
