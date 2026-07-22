using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Identifies a supported accelerometer calibration workflow.</summary>
public enum AccelerometerCalibrationKind
{
    /// <summary>Six-position accelerometer calibration.</summary>
    SixPosition,
    /// <summary>Level-board trim calibration.</summary>
    Level
}

/// <summary>Identifies the terminal and active stages of a calibration workflow.</summary>
public enum CalibrationWorkflowState
{
    /// <summary>No calibration has started.</summary>
    NotStarted,
    /// <summary>The start command is awaiting protocol acknowledgement.</summary>
    Preparing,
    /// <summary>The vehicle requested a specific physical orientation.</summary>
    WaitingForOrientation,
    /// <summary>The vehicle is sampling the confirmed orientation.</summary>
    Sampling,
    /// <summary>The vehicle is completing calibration.</summary>
    Completing,
    /// <summary>The protocol explicitly confirmed success.</summary>
    Success,
    /// <summary>The protocol rejected or explicitly failed calibration.</summary>
    Failed,
    /// <summary>The user cancelled calibration.</summary>
    Cancelled,
    /// <summary>The vehicle disconnected during calibration.</summary>
    Disconnected
}

/// <summary>Identifies one physical orientation in six-position accelerometer calibration.</summary>
public enum CalibrationOrientation
{
    /// <summary>Vehicle level on its landing gear.</summary>
    Level = 1,
    /// <summary>Vehicle resting on its left side.</summary>
    Left = 2,
    /// <summary>Vehicle resting on its right side.</summary>
    Right = 3,
    /// <summary>Vehicle nose pointing down.</summary>
    NoseDown = 4,
    /// <summary>Vehicle nose pointing up.</summary>
    NoseUp = 5,
    /// <summary>Vehicle resting upside down on its back.</summary>
    Back = 6
}

/// <summary>Represents the immutable state projected by the calibration UI.</summary>
/// <param name="VehicleId">The target vehicle, when a run exists.</param>
/// <param name="Kind">The selected calibration kind.</param>
/// <param name="State">The current workflow stage.</param>
/// <param name="RequiredOrientation">The orientation explicitly requested by the vehicle.</param>
/// <param name="CompletedOrientations">Orientations accepted before the current request.</param>
/// <param name="Progress">Normalized protocol progress from zero to one.</param>
/// <param name="Instruction">The primary unambiguous user instruction.</param>
/// <param name="SupplementalStatus">The latest relevant STATUSTEXT message.</param>
/// <param name="FailureReason">The terminal failure or disconnect explanation.</param>
public sealed record CalibrationSnapshot(
    VehicleId? VehicleId,
    AccelerometerCalibrationKind? Kind,
    CalibrationWorkflowState State,
    CalibrationOrientation? RequiredOrientation,
    IReadOnlySet<CalibrationOrientation> CompletedOrientations,
    double Progress,
    string Instruction,
    string? SupplementalStatus = null,
    string? FailureReason = null)
{
    /// <summary>Gets the initial calibration state.</summary>
    public static CalibrationSnapshot Initial { get; } = new(
        null, null, CalibrationWorkflowState.NotStarted, null,
        new HashSet<CalibrationOrientation>(), 0,
        "Choose six-position accelerometer calibration or level calibration.");
}

/// <summary>Provides a calibration state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class CalibrationStateChangedEventArgs(CalibrationSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new calibration state.</summary>
    public CalibrationSnapshot Snapshot { get; } = snapshot;
}
