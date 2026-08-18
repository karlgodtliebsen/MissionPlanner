namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects the live readings of one battery instance.</summary>
/// <param name="VoltageVolts">The live voltage, when reported.</param>
/// <param name="CurrentAmps">The live current, when reported.</param>
/// <param name="ConsumedMah">The consumed capacity, when reported.</param>
/// <param name="RemainingPercent">The estimated remaining percentage, when reported.</param>
/// <param name="IsStale">Whether the telemetry is older than the freshness window.</param>
/// <param name="HasTelemetry">Whether any live telemetry is available for this instance.</param>
public sealed record BatteryLiveReading(
    double? VoltageVolts,
    double? CurrentAmps,
    double? ConsumedMah,
    int? RemainingPercent,
    bool IsStale,
    bool HasTelemetry);
