namespace MissionPlanner.Core.Setup;

/// <summary>Describes one discovered battery monitor instance built from parameters and telemetry.</summary>
/// <param name="Index">The one-based battery instance index.</param>
/// <param name="MonitorType">The configured monitor backend value.</param>
/// <param name="MonitorName">The human-readable monitor backend name.</param>
/// <param name="Values">The available numeric settings keyed by kind.</param>
/// <param name="Live">The live readings for this instance.</param>
public sealed record BatteryMonitorInstance(
    int Index,
    int MonitorType,
    string MonitorName,
    IReadOnlyDictionary<BatterySetting, double> Values,
    BatteryLiveReading Live)
{
    /// <summary>Gets the stored value for a setting, when present.</summary>
    /// <param name="setting">The setting kind.</param>
    /// <returns>The stored value, or null when the parameter is absent.</returns>
    public double? Get(BatterySetting setting)
    {
        return Values.TryGetValue(setting, out var value) ? value : null;
    }
}
