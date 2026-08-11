using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Aggregates registered optional-hardware modules and filters by parameter presence.</summary>
public sealed class OptionalHardwareCatalog : IOptionalHardwareCatalog
{
    /// <summary>Initializes the catalog from the registered modules.</summary>
    /// <param name="modules">The registered optional-hardware modules.</param>
    public OptionalHardwareCatalog(IEnumerable<IOptionalHardwareModule> modules)
    {
        Modules = modules.OrderBy(module => module.Title, StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<IOptionalHardwareModule> Modules { get; }

    /// <inheritdoc />
    public IReadOnlyList<IOptionalHardwareModule> GetAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return Modules.Where(module => module.IsAvailable(parameters)).ToArray();
    }
}
