using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Workflow;

/// <summary>Physical connection selected for firmware work, independent of telemetry.</summary>
public enum FirmwarePhysicalTarget
{
    /// <summary>No controller selected.</summary>
    None,
    /// <summary>A serial controller.</summary>
    Serial,
    /// <summary>A controller enumerated in STM32 ROM DFU.</summary>
    Stm32Dfu
}

/// <summary>Runtime identified by a live protocol exchange.</summary>
public enum FirmwareRuntimeKind
{
    /// <summary>No application is running or selected.</summary>
    None,
    /// <summary>ArduPilot identified by protocol.</summary>
    ArduPilot,
    /// <summary>Betaflight identified by MSP.</summary>
    Betaflight,
    /// <summary>Runtime has not been identified.</summary>
    Unknown
}

/// <summary>Observed boot environment, separate from application identity.</summary>
public enum FirmwareBootEnvironment
{
    /// <summary>No boot environment has been proven.</summary>
    None,
    /// <summary>ArduPilot serial bootloader synchronization succeeded.</summary>
    ArduPilotBootloader,
    /// <summary>STM32 ROM USB DFU is present.</summary>
    Stm32RomDfu
}

/// <summary>Strength of exact flight-controller target evidence.</summary>
public enum FirmwareIdentityConfidence
{
    /// <summary>No target evidence.</summary>
    Unknown,
    /// <summary>USB or product text only.</summary>
    Hint,
    /// <summary>Explicitly selected candidate requiring safety confirmation.</summary>
    Candidate,
    /// <summary>Protocol board ID or reviewed exact mapping.</summary>
    Verified
}

/// <summary>Artifact families supported by the ArduPilot installation workflow.</summary>
public enum FirmwareArtifactFormat
{
    /// <summary>No format has been resolved.</summary>
    None,
    /// <summary>ArduPilot application JSON package.</summary>
    Apj,
    /// <summary>Combined bootloader and application Intel HEX.</summary>
    WithBootloaderHex
}

/// <summary>Immutable evidence used to resolve preparation, entry and installation capabilities.</summary>
public sealed record FirmwareWorkflowContext
{
    /// <summary>Gets whether this host supports hardware operations.</summary>
    public bool HardwareSupported { get; init; } = true;
    /// <summary>Gets the selected physical connection.</summary>
    public FirmwarePhysicalTarget PhysicalTarget { get; init; }
    /// <summary>Gets the physical endpoint or stable identity.</summary>
    public string? Endpoint { get; init; }
    /// <summary>Gets the protocol-observed runtime.</summary>
    public FirmwareRuntimeKind Runtime { get; init; }
    /// <summary>Gets the observed boot environment.</summary>
    public FirmwareBootEnvironment BootEnvironment { get; init; }
    /// <summary>Gets the strength of board identity evidence.</summary>
    public FirmwareIdentityConfidence IdentityConfidence { get; init; }
    /// <summary>Gets the evidence supporting board identity.</summary>
    public string? IdentityEvidence { get; init; }
    /// <summary>Gets the protocol-reported bootloader identity.</summary>
    public BootloaderIdentity? Bootloader { get; init; }
    /// <summary>Gets the selected ArduPilot platform.</summary>
    public string? Platform { get; init; }
    /// <summary>Gets the selected firmware board ID.</summary>
    public int? BoardId { get; init; }
    /// <summary>Gets the selected catalogue release, if any.</summary>
    public FirmwareManifestEntry? Release { get; init; }
    /// <summary>Gets the unrelated or target-specific telemetry transport.</summary>
    public ConnectionTransportKind? ActiveConnection { get; init; }
    /// <summary>Gets whether the selected resource is owned by a telemetry session.</summary>
    public bool TargetPortOwned { get; init; }
    /// <summary>Gets whether a firmware operation owns the operation lease.</summary>
    public bool OperationInProgress { get; init; }
    /// <summary>Gets whether the selected target is known armed.</summary>
    public bool TargetArmed { get; init; }
    /// <summary>Gets the prepared artifact format.</summary>
    public FirmwareArtifactFormat ArtifactFormat { get; init; }
    /// <summary>Gets whether structural validation succeeded.</summary>
    public bool ArtifactValid { get; init; }
    /// <summary>Gets whether target-specific compatibility checks succeeded.</summary>
    public bool TargetCompatible { get; init; }
    /// <summary>Gets whether required DFU target confirmation has been explicitly accepted.</summary>
    public bool TargetSafetyConfirmed { get; init; }
}
