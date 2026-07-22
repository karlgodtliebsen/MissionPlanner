using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Discovers battery monitor instances and applies guarded, readback-confirmed battery edits.</summary>
public interface IBatteryConfigurationService
{
    /// <summary>Builds the battery configuration from live parameters, metadata, and telemetry.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>The discovered battery configuration.</returns>
    Task<BatteryConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms one battery setting, rejecting invalid failsafe combinations.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="instance">The one-based battery instance index.</param>
    /// <param name="setting">The setting to write.</param>
    /// <param name="value">The proposed value.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or rejected apply result.</returns>
    Task<BatteryApplyResult> SetValueAsync(VehicleId vehicleId, int instance, BatterySetting setting, double value, CancellationToken cancellationToken = default);

    /// <summary>Computes and writes a corrected voltage multiplier from a measured and reference voltage.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="instance">The one-based battery instance index.</param>
    /// <param name="measuredVolts">The voltage reported by the vehicle.</param>
    /// <param name="referenceVolts">The voltage measured with an external meter.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or rejected apply result.</returns>
    Task<BatteryApplyResult> CalibrateVoltageAsync(VehicleId vehicleId, int instance, double measuredVolts, double referenceVolts, CancellationToken cancellationToken = default);

    /// <summary>Computes and writes a corrected amperes-per-volt scale from a measured and reference current.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="instance">The one-based battery instance index.</param>
    /// <param name="measuredAmps">The current reported by the vehicle.</param>
    /// <param name="referenceAmps">The current measured with an external meter.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or rejected apply result.</returns>
    Task<BatteryApplyResult> CalibrateCurrentAsync(VehicleId vehicleId, int instance, double measuredAmps, double referenceAmps, CancellationToken cancellationToken = default);

    /// <summary>Requests refreshed values for the battery parameter set.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after refresh requests are sent.</returns>
    Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
