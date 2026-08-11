namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>
/// Provides the explicit promoted telemetry descriptor set.
/// </summary>
public interface ITelemetryFieldCatalog
{
    /// <summary>
    /// Gets the collection of telemetry field descriptors.
    /// </summary>
    IReadOnlyList<TelemetryFieldDescriptor> Fields { get; }
}
