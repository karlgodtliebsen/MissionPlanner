using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Records one auditable control event against simulation time and vehicle identity.</summary>
/// <param name="Timestamp">Wall-clock event time.</param>
/// <param name="SimulationTime">Elapsed simulation session time.</param>
/// <param name="SessionId">Simulation session identity.</param>
/// <param name="VehicleId">Exact target vehicle.</param>
/// <param name="ControlKey">Logical control key.</param>
/// <param name="ParameterName">Resolved parameter name.</param>
/// <param name="RequestedValue">Requested or reset value.</param>
/// <param name="Result">Operation result.</param>
/// <param name="Message">Result detail.</param>
public sealed record SimulationScenarioEvent(
    DateTimeOffset Timestamp,
    TimeSpan SimulationTime,
    Guid SessionId,
    VehicleId VehicleId,
    string ControlKey,
    string ParameterName,
    double RequestedValue,
    SimulationScenarioEventResult Result,
    string Message);
