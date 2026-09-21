namespace MissionPlanner.Firmware.Connected;

/// <summary>Defines a connected embedded bootloader update request.</summary>
public sealed record BootloaderUpdateRequest(bool WarningAccepted)
{
    /// <summary>Gets selected target and independently observed device identities.</summary>
    public MissionPlanner.Firmware.Model.FirmwareIdentitySnapshot? Identity { get; init; }

    /// <summary>Gets whether a combined DFU recovery image is selected.</summary>
    public bool UsesCombinedDfuImage { get; init; }
}
