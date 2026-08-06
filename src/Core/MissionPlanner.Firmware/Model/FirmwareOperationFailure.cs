namespace MissionPlanner.Firmware.Model;

/// <summary>Describes a firmware operation failure without UI text.</summary>
public sealed record FirmwareOperationFailure(
    string Code,
    FirmwareOperationState Stage,
    string? TechnicalDetail = null,
    string? ExceptionType = null);
