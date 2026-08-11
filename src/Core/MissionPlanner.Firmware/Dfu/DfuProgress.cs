namespace MissionPlanner.Firmware.Dfu;

/// <summary>Reports DFU operation progress.</summary>
public sealed record DfuProgress(
    DfuOperationState State,
    string MessageCode,
    double? Percentage = null,
    long? CompletedBytes = null,
    long? TotalBytes = null,
    string? TechnicalDetail = null);
