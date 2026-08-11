using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Provides firmware-aware auxiliary-function descriptors.</summary>
public interface IAuxiliaryFunctionCatalog
{
    /// <summary>Returns the reviewed catalog. Unknown IDs can be represented with <see cref="DescribeUnknown"/>.</summary>
    IReadOnlyList<AuxiliaryFunctionDescriptor> GetFunctions(VehicleState vehicle);

    /// <summary>Creates a non-executable descriptor for an ID absent from the reviewed catalog.</summary>
    AuxiliaryFunctionDescriptor DescribeUnknown(int id);
}
