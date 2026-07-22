using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Runs ArduPilot accelerometer calibration protocols behind a domain-facing state machine.</summary>
public interface IArduPilotCalibrationService : IDisposable
{
    /// <summary>Gets the current immutable calibration state.</summary>
    CalibrationSnapshot Current { get; }

    /// <summary>Occurs when protocol evidence advances or terminates calibration.</summary>
    event EventHandler<CalibrationStateChangedEventArgs>? StateChanged;

    /// <summary>Starts the six-position accelerometer workflow.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after the protocol accepts or rejects startup.</returns>
    Task StartSixPositionAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Starts level-board trim calibration.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after terminal command acknowledgement.</returns>
    Task StartLevelAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Confirms that the requested physical orientation is stable and ready to sample.</summary>
    /// <param name="cancellationToken">A token that cancels transmission.</param>
    /// <returns>A task that completes after the protocol response is sent.</returns>
    Task ConfirmOrientationAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels the active workflow and sends an abort indication when supported.</summary>
    /// <param name="cancellationToken">A token that cancels abort transmission.</param>
    /// <returns>A task that completes after local cancellation is stable.</returns>
    Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a terminal workflow to its initial recoverable state.</summary>
    void Reset();
}
