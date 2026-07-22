using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

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

/// <summary>Discovers available optional-hardware modules and applies guarded peripheral edits.</summary>
public interface IOptionalHardwareService
{
    /// <summary>Builds the available optional-hardware module projections for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>The available module projections.</returns>
    Task<IReadOnlyList<OptionalHardwareModuleView>> GetModulesAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms one peripheral parameter that belongs to an available module.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="parameterName">The parameter to write.</param>
    /// <param name="value">The proposed value.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or rejected apply result.</returns>
    Task<OptionalHardwareApplyResult> SetValueAsync(VehicleId vehicleId, string parameterName, double value, CancellationToken cancellationToken = default);

    /// <summary>Requests refreshed values for the parameters of all available modules.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after refresh requests are sent.</returns>
    Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
