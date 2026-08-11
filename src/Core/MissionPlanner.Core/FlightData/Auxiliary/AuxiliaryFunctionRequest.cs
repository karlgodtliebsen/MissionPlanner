using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Requests execution of an auxiliary function at a generated switch level.</summary>
public sealed record AuxiliaryFunctionRequest(
    VehicleState Vehicle,
    AuxiliaryFunctionDescriptor Function,
    MavCmdDoAuxFunctionSwitchLevel Level,
    bool Confirmed);
