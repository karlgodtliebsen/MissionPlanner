namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Typed board evidence; an MCU match alone never establishes firmware compatibility.</summary>
public sealed record BetaflightBoardInfo(string Identifier, ushort HardwareRevision, byte? BoardType = null,
    BetaflightTargetCapabilities? Capabilities = null, string? TargetName = null, string? BoardName = null,
    string? ManufacturerId = null, string? Signature = null, byte? McuId = null, byte? ConfigurationState = null);