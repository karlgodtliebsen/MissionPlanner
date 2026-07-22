using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Provides live RC channel projection and a guarded endpoint-calibration state machine.</summary>
public interface IRadioCalibrationService : IDisposable
{
    /// <summary>Gets the current immutable calibration state.</summary>
    RadioCalibrationSnapshot Current { get; }

    /// <summary>Occurs when calibration capture advances or terminates.</summary>
    event EventHandler<RadioCalibrationStateChangedEventArgs>? StateChanged;

    /// <summary>Projects the live RC channels and static configuration issues for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <returns>The live RC channel projection.</returns>
    RadioChannelsView GetLiveChannels(VehicleId vehicleId);

    /// <summary>Starts endpoint capture, recording the extremes of every moved channel.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes once capture has started.</returns>
    Task StartAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates and writes the captured endpoints, confirming each by readback.</summary>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed write result.</returns>
    Task<RadioWriteResult> CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels capture without writing any endpoints.</summary>
    /// <param name="cancellationToken">A token that cancels cancellation work.</param>
    /// <returns>A task that completes once cancellation is stable.</returns>
    Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a terminal workflow to its initial recoverable state.</summary>
    void Reset();
}
