using MissionPlanner.Firmware.Betaflight.Protocol;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Known BOARD_INFO capability bits; unknown bits remain in the underlying byte.</summary>
[Flags]
public enum BetaflightTargetCapabilities : byte
{
    /// <summary>No known capability reported.</summary>
    None = 0,
    /// <summary>USB virtual COM port.</summary>
    VirtualComPort = 1,
    /// <summary>Software serial support.</summary>
    SoftSerial = 2,
    /// <summary>Flash-resident bootloader; distinct from STM32 ROM DFU.</summary>
    FlashBootloader = 8,
    /// <summary>Receiver binding support.</summary>
    ReceiverBind = 64
}

/// <summary>Typed board evidence; an MCU match alone never establishes firmware compatibility.</summary>
public sealed record BetaflightBoardInfo(string Identifier, ushort HardwareRevision, byte? BoardType = null,
    BetaflightTargetCapabilities? Capabilities = null, string? TargetName = null, string? BoardName = null,
    string? ManufacturerId = null, string? Signature = null, byte? McuId = null, byte? ConfigurationState = null);

/// <summary>Proven runtime identity, kept separate from canonical USB/OS device metadata.</summary>
public sealed record BetaflightDeviceInfo(string PortName, Version MspApiVersion, string FirmwareVariant,
    Version? FirmwareVersion = null, string? FirmwareVersionLabel = null, BetaflightBoardInfo? Board = null,
    string? BuildInformation = null, string? SourceRevision = null, string? CraftName = null,
    string? McuType = null, string? McuUniqueId = null);

/// <summary>Outcome of an identity probe.</summary>
public enum BetaflightProbeOutcome
{
    /// <summary>Exact BTFL proof received.</summary>
    Success,
    /// <summary>No valid MSP reply.</summary>
    NotMsp,
    /// <summary>Another MSP firmware variant.</summary>
    MspButNotBetaflight,
    /// <summary>Port ownership could not be acquired.</summary>
    PortBusy,
    /// <summary>A probe deadline expired.</summary>
    Timeout,
    /// <summary>Device disconnected.</summary>
    Disconnected,
    /// <summary>Invalid mandatory identity payload.</summary>
    ProtocolError,
    /// <summary>Unsupported API major or host platform.</summary>
    UnsupportedApi,
    /// <summary>Caller cancelled discovery.</summary>
    Cancelled
}

/// <summary>Identity and typed optional diagnostics, without raw firmware payload logging.</summary>
public sealed record BetaflightProbeResult(BetaflightProbeOutcome Outcome, BetaflightDeviceInfo? Identity = null,
    MspFailure Failure = MspFailure.None, string? Diagnostic = null);

/// <summary>Proves firmware identity on one exclusively owned selected serial endpoint.</summary>
public interface IBetaflightDeviceProbe
{
    /// <summary>Probes and releases the endpoint, without bootloader commands or telemetry session creation.</summary>
    Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default);
}
