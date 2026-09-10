using MissionPlanner.Firmware.Entry;

namespace MissionPlanner.Firmware.Workflow;

/// <summary>The non-destructive transition required before flashing.</summary>
public enum FirmwareBootEntryRequirement
{
    /// <summary>No resolved entry strategy.</summary>
    None,
    /// <summary>The target is already in its required boot environment.</summary>
    AlreadyInBootloader,
    /// <summary>Use the existing ArduPilot entry strategies, including manual reconnect fallback.</summary>
    ArduPilotRebootOrManualReconnect,
    /// <summary>Live-verify Betaflight and request ROM DFU using MSP.</summary>
    BetaflightMsp,
    /// <summary>Guide the operator through the board's BOOT/RESET procedure.</summary>
    ManualBootReset
}

/// <summary>Immutable installation decision consumed by the firmware page and execution boundary.</summary>
/// <param name="Context">The evidence snapshot used for this decision.</param>
/// <param name="Capabilities">Available preparation, entry and install actions.</param>
/// <param name="Transport">Required existing bootloader transport, or unresolved.</param>
/// <param name="BootEntry">Required non-destructive boot transition.</param>
/// <param name="Description">Human-readable operation description.</param>
public sealed record FirmwareInstallationPlan(
    FirmwareWorkflowContext Context,
    FirmwareWorkflowCapabilities Capabilities,
    BootloaderEntryTarget? Transport,
    FirmwareBootEntryRequirement BootEntry,
    string Description)
{
    /// <summary>Gets whether compatibility and artifact requirements allow installation.</summary>
    public bool CanExecute => Capabilities.CanInstall;
    /// <summary>Gets the required artifact family.</summary>
    public FirmwareArtifactFormat RequiredArtifactFormat => Capabilities.RequiredArtifactFormat;
    /// <summary>Gets the stable blocking diagnostic code.</summary>
    public string? BlockCode => Capabilities.BlockCode;
    /// <summary>Gets the next step required before installation.</summary>
    public string? BlockReason => Capabilities.BlockReason;
    /// <summary>Gets the expected runtime after a verified flash and reboot.</summary>
    public FirmwareRuntimeKind ExpectedRuntime => FirmwareRuntimeKind.ArduPilot;
}
