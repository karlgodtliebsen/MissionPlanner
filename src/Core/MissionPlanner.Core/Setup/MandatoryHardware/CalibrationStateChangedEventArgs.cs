namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Provides a calibration state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class CalibrationStateChangedEventArgs(CalibrationSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new calibration state.</summary>
    public CalibrationSnapshot Snapshot { get; } = snapshot;
}
