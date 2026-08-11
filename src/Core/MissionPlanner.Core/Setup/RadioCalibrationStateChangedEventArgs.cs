namespace MissionPlanner.Core.Setup;

/// <summary>Provides a radio calibration state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class RadioCalibrationStateChangedEventArgs(RadioCalibrationSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new radio calibration state.</summary>
    public RadioCalibrationSnapshot Snapshot { get; } = snapshot;
}
