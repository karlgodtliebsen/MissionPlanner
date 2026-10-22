namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>The state of a CompassMot calibration.</summary>
/// <param name="State">The calibration state.</param>
/// <param name="Instruction">The instruction for the operator.</param>
/// <param name="Samples">The measurements collected so far.</param>
/// <param name="FailureReason">Why the calibration failed, or null.</param>
public sealed record CompassMotorCalibrationSnapshot(CompassMotorCalibrationState State, string Instruction, IReadOnlyList<CompassMotorCalibrationSample> Samples, string? FailureReason)
{
    /// <summary>Gets the snapshot used before a calibration has started.</summary>
    public static CompassMotorCalibrationSnapshot Initial { get; } = new(CompassMotorCalibrationState.Idle, "Remove all propellers before starting CompassMot.", [], null);
}