using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Provides live RC channel projection and a guarded endpoint-calibration state machine.</summary>
public interface IRadioCalibrationService : IDisposable
{
    /// <summary>Gets the current immutable calibration state.</summary>
    RadioCalibrationSnapshot Current
    {
        get;
    }

    /// <summary>Occurs when calibration capture advances or terminates.</summary>
    event Action<RadioCalibrationStateChangedEventArgs>? StateChanged;

    /// <summary>Projects the live RC channels and static configuration issues for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <returns>The live RC channel projection.</returns>
    RadioChannelsView GetLiveChannels(VehicleId vehicleId);

    /// <summary>Starts endpoint capture, recording the extremes of every moved channel.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes once capture has started.</returns>
    Task StartAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Stops endpoint discovery, validates travel, and enters Review without writing parameters.</summary>
    /// <param name="cancellationToken">A token that cancels the transition.</param>
    /// <returns>The resulting Review or recoverable Capturing snapshot.</returns>
    Task<RadioCalibrationSnapshot> FinishCaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>Samples fresh Review-stage trims, then writes and confirms the complete calibration.</summary>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed write result.</returns>
    Task<RadioWriteResult> CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels capture or review without writing any endpoints.</summary>
    /// <param name="cancellationToken">A token that cancels cancellation work.</param>
    /// <returns>A task that completes once cancellation is stable.</returns>
    Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a terminal workflow to its initial recoverable state.</summary>
    void Reset();
}
