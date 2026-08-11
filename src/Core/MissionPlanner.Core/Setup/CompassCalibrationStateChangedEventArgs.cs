namespace MissionPlanner.Core.Setup;

/// <summary>Provides a compass calibration state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class CompassCalibrationStateChangedEventArgs(CompassCalibrationSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new compass calibration state.</summary>
    public CompassCalibrationSnapshot Snapshot { get; } = snapshot;
}
