namespace MissionPlanner.Firmware.Dfu;

/// <summary>Describes a typed DFU failure.</summary>
public sealed record DfuFailure(string Code, DfuOperationState Stage, string Message, string? TechnicalDetail = null);
