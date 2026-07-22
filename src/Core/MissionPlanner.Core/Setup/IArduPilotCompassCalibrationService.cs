using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Runs the ArduPilot onboard compass calibration protocol behind a domain-facing state machine.</summary>
public interface IArduPilotCompassCalibrationService : IDisposable
{
    /// <summary>Gets the current immutable calibration state.</summary>
    CompassCalibrationSnapshot Current { get; }

    /// <summary>Occurs when protocol evidence advances or terminates calibration.</summary>
    event EventHandler<CompassCalibrationStateChangedEventArgs>? StateChanged;

    /// <summary>Starts onboard calibration for all enabled compasses.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="autoSave">Whether the vehicle should persist results without an explicit acceptance step.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after the protocol accepts or rejects startup.</returns>
    Task StartAsync(VehicleId vehicleId, bool autoSave, CancellationToken cancellationToken = default);

    /// <summary>Accepts pending calibration results so the vehicle saves the computed offsets.</summary>
    /// <param name="cancellationToken">A token that cancels transmission.</param>
    /// <returns>A task that completes after the acceptance command is sent.</returns>
    Task AcceptAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels the active calibration and instructs the vehicle to discard progress.</summary>
    /// <param name="cancellationToken">A token that cancels abort transmission.</param>
    /// <returns>A task that completes after local cancellation is stable.</returns>
    Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a terminal workflow to its initial recoverable state.</summary>
    void Reset();
}
