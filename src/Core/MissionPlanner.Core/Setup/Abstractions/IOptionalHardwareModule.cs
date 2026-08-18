using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>
/// Defines one optional-hardware setup module. Modules are discovered from parameter presence so
/// peripherals can be added without modifying a central switch.
/// </summary>
public interface IOptionalHardwareModule
{
    /// <summary>Gets the stable module key.</summary>
    string Key { get; }

    /// <summary>Gets the module title.</summary>
    string Title { get; }

    /// <summary>Determines whether the module applies to the connected vehicle's parameters.</summary>
    /// <param name="parameters">The live parameter set.</param>
    /// <returns><see langword="true"/> when the peripheral parameters are present.</returns>
    bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters);

    /// <summary>Builds the module projection from live parameters and metadata.</summary>
    /// <param name="parameters">The live parameter set.</param>
    /// <param name="metadata">The firmware parameter metadata.</param>
    /// <returns>The module projection.</returns>
    OptionalHardwareModuleView Build(
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata);
}
