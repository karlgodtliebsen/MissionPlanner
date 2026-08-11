using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

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
