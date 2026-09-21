namespace MissionPlanner.Firmware.Model;

/// <summary>Represents a compatibility decision with a stable reason code.</summary>
public sealed record FirmwareCompatibilityResult(bool IsCompatible, string Code, string? TechnicalDetail = null)
{
    /// <summary>Gets the structured source-aware decision.</summary>
    public FirmwareCompatibilityStatus Status { get; init; } = IsCompatible ? FirmwareCompatibilityStatus.Compatible : FirmwareCompatibilityStatus.BootloaderMismatch;
    /// <summary>Gets the explicitly requested operation mode.</summary>
    public FirmwareInstallMode Mode { get; init; }
    /// <summary>Gets the human-readable explanation.</summary>
    public string Summary { get; init; } = TechnicalDetail ?? Code;
    /// <summary>Gets independently attributed evidence.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];
    /// <summary>Gets whether identity policy permits continuing to package checks and confirmation.</summary>
    public bool CanProceed => IsCompatible;
    /// <summary>Gets whether explicit target replacement confirmation is mandatory.</summary>
    public bool RequiresExplicitConfirmation { get; init; }
    /// <summary>Gets whether querying the bootloader in an explicit recovery workflow is appropriate.</summary>
    public bool CanOfferRecovery { get; init; }
}
