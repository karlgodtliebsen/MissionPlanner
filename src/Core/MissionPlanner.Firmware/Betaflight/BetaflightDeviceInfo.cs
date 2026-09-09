namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Proven runtime identity, kept separate from canonical USB/OS device metadata.</summary>
public sealed record BetaflightDeviceInfo(string PortName, Version MspApiVersion, string FirmwareVariant,
    Version? FirmwareVersion = null, string? FirmwareVersionLabel = null, BetaflightBoardInfo? Board = null,
    string? BuildInformation = null, string? SourceRevision = null, string? CraftName = null,
    string? McuType = null, string? McuUniqueId = null);