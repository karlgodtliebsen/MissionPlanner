namespace MissionPlanner.Firmware.Workflow;

/// <summary>Protocol-observed runtime and boot state for one physical presence generation.</summary>
/// <param name="Runtime">Application runtime proven by protocol.</param>
/// <param name="BootEnvironment">Observed boot environment.</param>
/// <param name="Code">Stable diagnostic outcome.</param>
/// <param name="IsArmed">Live armed evidence when available.</param>
public sealed record FirmwareRuntimeProbeResult(FirmwareRuntimeKind Runtime, FirmwareBootEnvironment BootEnvironment,
    string Code, bool? IsArmed = null)
{
    /// <summary>Gets the source of the runtime evidence, kept separate from exact-board identity.</summary>
    public FirmwareRuntimeEvidence Evidence { get; init; } = FirmwareRuntimeEvidence.None;

    /// <summary>Gets how strongly the runtime is identified. This never proves the exact board.</summary>
    public FirmwareRuntimeVerification Verification { get; init; } = FirmwareRuntimeVerification.None;

    /// <summary>Gets the optional protocol/version detail observed while probing, when available.</summary>
    public string? ProtocolDetail { get; init; }

    /// <summary>Gets the typed result of the protocol attempt.</summary>
    public FirmwareRuntimeProbeOutcome Outcome { get; init; }

    /// <summary>Gets the operating mode independently of exact board identity.</summary>
    public string OperatingMode => BootEnvironment != FirmwareBootEnvironment.None
        ? BootEnvironment.ToString()
        : Verification == FirmwareRuntimeVerification.Verified && Runtime is FirmwareRuntimeKind.ArduPilot or FirmwareRuntimeKind.Betaflight
            ? "Application" : "Unknown";
}

/// <summary>Distinguishes protocol evidence from unavailable identification.</summary>
public enum FirmwareRuntimeProbeOutcome
{
    /// <summary>No attempt has completed.</summary>
    NotProbed,
    /// <summary>The application protocol was identified.</summary>
    Success,
    /// <summary>No qualifying heartbeat arrived.</summary>
    Timeout,
    /// <summary>A different MAVLink autopilot responded.</summary>
    OtherAutopilot,
    /// <summary>The endpoint is exclusively owned.</summary>
    PortBusy,
    /// <summary>The transport failed.</summary>
    TransportError,
    /// <summary>MSP did not identify Betaflight.</summary>
    NotBetaflight,
    /// <summary>The attempt was cancelled.</summary>
    Cancelled
}
