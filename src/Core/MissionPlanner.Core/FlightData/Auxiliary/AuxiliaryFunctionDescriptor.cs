namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Describes one reviewed ArduPilot auxiliary function.</summary>
public sealed record AuxiliaryFunctionDescriptor(
    int Id,
    string Name,
    string Description,
    string Category,
    AuxiliarySwitchBehavior SwitchBehavior,
    AuxiliaryFunctionHazard Hazard,
    string? PreferredWorkflow = null,
    bool IsSupported = true);
