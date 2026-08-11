using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Reviewed baseline catalog; parameter-derived unknown IDs remain explicit and disabled.</summary>
public sealed class AuxiliaryFunctionCatalog : IAuxiliaryFunctionCatalog
{
    private static readonly AuxiliaryFunctionDescriptor[] functions =
    [
        new(28, "Relay 1", "Operate relay output 1.", "Actuator", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.Warning, "Servo / Relay", false),
        new(9, "Camera trigger", "Trigger the selected camera.", "Payload", AuxiliarySwitchBehavior.Momentary,
            AuxiliaryFunctionHazard.Safe, "Payload Control", false),
        new(31, "Motor emergency stop", "Emergency motor stop control.", "Emergency", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.High, null, false),
        new(27, "Landing gear", "Set landing gear switch state.", "Vehicle", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.Warning),
        new(65, "GPS disable", "Temporarily disable GPS use.", "Sensors", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.High, null, false),
        new(300, "Lost vehicle sound", "Activate the configured lost-vehicle sound.", "Notification",
            AuxiliarySwitchBehavior.ThreePosition, AuxiliaryFunctionHazard.Safe)
    ];

    /// <inheritdoc />
    public IReadOnlyList<AuxiliaryFunctionDescriptor> GetFunctions(VehicleState vehicle)
    {
        return functions;
    }

    /// <inheritdoc />
    public AuxiliaryFunctionDescriptor DescribeUnknown(int id)
    {
        return new AuxiliaryFunctionDescriptor(id, $"Function {id}",
            "This function is not in the reviewed catalog for this MissionPlanner version.", "Unknown",
            AuxiliarySwitchBehavior.ThreePosition, AuxiliaryFunctionHazard.High, null, false);
    }
}
