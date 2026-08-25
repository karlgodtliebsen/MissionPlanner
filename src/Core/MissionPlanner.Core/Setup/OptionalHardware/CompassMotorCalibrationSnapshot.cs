namespace MissionPlanner.Core.Setup.OptionalHardware;

public sealed record CompassMotorCalibrationSnapshot(CompassMotorCalibrationState State, string Instruction, IReadOnlyList<CompassMotorCalibrationSample> Samples, string? FailureReason)
{
    public static CompassMotorCalibrationSnapshot Initial { get; } = new(CompassMotorCalibrationState.Idle, "Remove all propellers before starting CompassMot.", [], null);
}