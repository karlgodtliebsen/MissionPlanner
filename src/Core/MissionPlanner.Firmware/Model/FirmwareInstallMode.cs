namespace MissionPlanner.Firmware.Model;

/// <summary>Explicit operation intent; recovery is never inferred from a mismatch.</summary>
public enum FirmwareInstallMode
{
    /// <summary>Strict upgrade of the currently running target.</summary>
    NormalUpgrade,
    /// <summary>Explicit replacement of incorrectly installed firmware.</summary>
    Recovery
}

/// <summary>Source-aware compatibility outcome.</summary>
public enum FirmwareCompatibilityStatus
{
    /// <summary>Compatible.</summary>
    Compatible,
    /// <summary>Compatible For Recovery.</summary>
    CompatibleForRecovery,
    /// <summary>Running Firmware Mismatch.</summary>
    RunningFirmwareMismatch,
    /// <summary>Bootloader Mismatch.</summary>
    BootloaderMismatch,
    /// <summary>Physical Device Mismatch.</summary>
    PhysicalDeviceMismatch,
    /// <summary>Target Mismatch.</summary>
    TargetMismatch,
    /// <summary>Vehicle Type Mismatch.</summary>
    VehicleTypeMismatch,
    /// <summary>Identity Insufficient.</summary>
    IdentityInsufficient,
    /// <summary>Ambiguous.</summary>
    Ambiguous
}
