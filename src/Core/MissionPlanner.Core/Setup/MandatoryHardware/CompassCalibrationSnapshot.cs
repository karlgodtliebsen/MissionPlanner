using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the immutable state projected by the compass calibration UI.</summary>
/// <param name="VehicleId">The target vehicle, when a run exists.</param>
/// <param name="State">The current workflow stage.</param>
/// <param name="Progress">The live per-compass progress ordered by compass identifier.</param>
/// <param name="Reports">The received per-compass reports ordered by compass identifier.</param>
/// <param name="OverallProgress">The normalized aggregate progress from zero to one.</param>
/// <param name="Instruction">The primary unambiguous user instruction.</param>
/// <param name="RequiresAcceptance">Whether successful results still need explicit acceptance.</param>
/// <param name="QualitySummary">A post-calibration quality summary when available.</param>
/// <param name="FailureReason">The terminal failure or disconnect explanation.</param>
public sealed record CompassCalibrationSnapshot(
    VehicleId? VehicleId,
    CompassCalibrationWorkflowState State,
    IReadOnlyList<CompassCalibrationProgress> Progress,
    IReadOnlyList<CompassCalibrationReport> Reports,
    double OverallProgress,
    string Instruction,
    bool RequiresAcceptance,
    string? QualitySummary = null,
    string? FailureReason = null)
{
    /// <summary>Gets the initial compass calibration state.</summary>
    public static CompassCalibrationSnapshot Initial { get; } = new(
        null, CompassCalibrationWorkflowState.NotStarted, [], [], 0,
        "Keep the vehicle clear of metal and magnetic interference, then start onboard calibration.",
        false);
}
