namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Reviewed exact board mapping. No default mappings are shipped without physical evidence.</summary>
public sealed record BetaflightArduPilotMapping(string BoardIdentifier, string TargetName, string BoardName,
    string ManufacturerId, ushort HardwareRevision, string ArduPilotPlatform, int ArduPilotBoardId,
    string ReviewEvidence, DateOnly ReviewedOn);