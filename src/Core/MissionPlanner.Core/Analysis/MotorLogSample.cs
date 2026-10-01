namespace MissionPlanner.Core.Analysis;

/// <summary>Logged speed or output measurement; source distinguishes units.</summary>
/// <param name="Index">Logged motor/channel/sensor index, with no inferred motor mapping.</param>
/// <param name="Source">ESC RPM, RPM sensor, or output PWM.</param>
/// <param name="TimeSeconds">Boot-time timestamp.</param>
/// <param name="Value">RPM for actual speed records, microseconds for output PWM.</param>
/// <param name="IsRpm">Whether the value is actual RPM and can predict shaft frequency.</param>
public sealed record MotorLogSample(int Index, string Source, double TimeSeconds, double Value, bool IsRpm);