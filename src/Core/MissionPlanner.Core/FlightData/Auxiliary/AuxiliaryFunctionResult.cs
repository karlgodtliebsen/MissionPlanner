using MissionPlanner.Core.Commands;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Reports an auxiliary-function outcome without implying observed state.</summary>
public sealed record AuxiliaryFunctionResult(VehicleCommandResponse? Acknowledgement, bool IsAccepted, string Summary);
