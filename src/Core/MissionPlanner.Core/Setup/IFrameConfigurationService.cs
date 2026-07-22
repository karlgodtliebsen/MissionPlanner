using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Discovers and safely applies firmware-supported frame configuration.</summary>
public interface IFrameConfigurationService
{
    /// <summary>Gets supported frame settings and opt-in recommendations from live values and metadata.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels discovery.</param>
    /// <returns>The supported frame configuration.</returns>
    Task<FrameConfigurationSnapshot> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Applies explicitly reviewed changes and confirms every write by readback.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="changes">The user-approved changes.</param>
    /// <param name="cancellationToken">A token tied to the active connection.</param>
    /// <returns>The confirmed result and any rollback guidance.</returns>
    Task<FrameConfigurationApplyResult> ApplyAsync(
        VehicleId vehicleId,
        IReadOnlyList<FrameParameterChange> changes,
        CancellationToken cancellationToken = default);

    /// <summary>Requests fresh values for the frame parameters supported by the connected vehicle.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels the refresh.</param>
    /// <returns>A task that completes when all requests have been sent.</returns>
    Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
