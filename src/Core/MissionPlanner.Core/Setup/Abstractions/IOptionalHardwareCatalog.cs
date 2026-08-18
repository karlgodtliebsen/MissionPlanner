using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Aggregates the registered optional-hardware modules.</summary>
public interface IOptionalHardwareCatalog
{
    /// <summary>Gets all registered modules.</summary>
    IReadOnlyList<IOptionalHardwareModule> Modules { get; }

    /// <summary>Returns the modules whose parameters are present on the vehicle.</summary>
    /// <param name="parameters">The live parameter set.</param>
    /// <returns>The available modules.</returns>
    IReadOnlyList<IOptionalHardwareModule> GetAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters);
}
