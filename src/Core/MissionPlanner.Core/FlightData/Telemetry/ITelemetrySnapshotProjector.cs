using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Projects and formats descriptors for the selected unit system.</summary>
public interface ITelemetrySnapshotProjector
{
    /// <summary>Projects one descriptor.</summary>
    TelemetryValueSnapshot Project(TelemetryFieldDescriptor descriptor, VehicleState state, UnitSystem units, DateTimeOffset now);
}
