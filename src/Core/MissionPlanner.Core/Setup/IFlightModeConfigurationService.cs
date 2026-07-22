using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Projects firmware flight-mode slot configuration and applies guarded slot writes.</summary>
public interface IFlightModeConfigurationService
{
    /// <summary>Builds the flight-mode slot configuration for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <returns>The flight-mode configuration projection.</returns>
    FlightModeConfiguration GetConfiguration(VehicleId vehicleId);

    /// <summary>Writes and confirms the firmware mode assigned to one slot.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="slot">The one-based slot number.</param>
    /// <param name="modeNumber">The firmware custom-mode number to assign.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<FlightModeApplyResult> SetSlotAsync(VehicleId vehicleId, int slot, int modeNumber, CancellationToken cancellationToken = default);

    /// <summary>Requests refreshed values for the flight-mode parameter set.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after refresh requests are sent.</returns>
    Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
