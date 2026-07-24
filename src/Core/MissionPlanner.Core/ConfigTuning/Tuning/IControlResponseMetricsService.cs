using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Stores the latest read-only PID response metric for each connected vehicle axis.</summary>
public interface IControlResponseMetricsService
{
    /// <summary>Occurs when a PID response metric is received.</summary>
    event EventHandler<ControlResponseMetricChangedEventArgs>? Changed;

    /// <summary>Gets the latest metrics for one vehicle.</summary>
    /// <param name="vehicleId">The vehicle.</param>
    /// <returns>The latest metric per reported axis.</returns>
    IReadOnlyList<ControlResponseMetric> GetMetrics(VehicleId vehicleId);
}
