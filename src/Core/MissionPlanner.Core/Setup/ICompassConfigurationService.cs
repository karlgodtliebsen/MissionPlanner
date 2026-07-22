using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Discovers compass instances and applies guarded, readback-confirmed compass parameter edits.</summary>
public interface ICompassConfigurationService
{
    /// <summary>Builds the current compass inventory from live parameters, device IDs, and health.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>The discovered compass inventory.</returns>
    Task<CompassInventory> GetInventoryAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms the board orientation of one compass.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="index">The one-based compass slot index.</param>
    /// <param name="orientationValue">The MAV_SENSOR_ORIENTATION value to store.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<CompassParameterApplyResult> SetOrientationAsync(VehicleId vehicleId, int index, int orientationValue, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms whether one compass is used for navigation.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="index">The one-based compass slot index.</param>
    /// <param name="use">Whether the compass should be enabled.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<CompassParameterApplyResult> SetUseAsync(VehicleId vehicleId, int index, bool use, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms whether one compass is marked external.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="index">The one-based compass slot index.</param>
    /// <param name="external">Whether the compass should be marked external.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<CompassParameterApplyResult> SetExternalAsync(VehicleId vehicleId, int index, bool external, CancellationToken cancellationToken = default);

    /// <summary>Determines whether disabling the given compass would leave no enabled compass.</summary>
    /// <param name="inventory">The current inventory.</param>
    /// <param name="index">The one-based compass slot index that would be disabled.</param>
    /// <returns><see langword="true"/> when the change removes the last enabled compass.</returns>
    bool WouldDisableOnlyEnabledCompass(CompassInventory inventory, int index);

    /// <summary>Requests refreshed values for the compass parameter set.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after refresh requests are sent.</returns>
    Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}

/// <summary>Represents the outcome of a confirmed compass parameter write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new value by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record CompassParameterApplyResult(bool Success, string Message);
