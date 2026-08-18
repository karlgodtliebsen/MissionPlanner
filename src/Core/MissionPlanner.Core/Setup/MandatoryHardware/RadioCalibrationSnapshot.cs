using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the immutable state projected by the radio calibration UI.</summary>
/// <param name="VehicleId">The target vehicle, when a run exists.</param>
/// <param name="State">The current workflow stage.</param>
/// <param name="Captures">The captured per-channel endpoints.</param>
/// <param name="Instruction">The primary unambiguous user instruction.</param>
/// <param name="Issues">Validation issues raised at review time.</param>
/// <param name="FailureReason">The terminal failure or disconnect explanation.</param>
public sealed record RadioCalibrationSnapshot(
    VehicleId? VehicleId,
    RadioCalibrationState State,
    IReadOnlyList<RadioChannelCapture> Captures,
    string Instruction,
    IReadOnlyList<RadioValidationIssue> Issues,
    string? FailureReason = null)
{
    /// <summary>Gets the initial radio calibration state.</summary>
    public static RadioCalibrationSnapshot Initial { get; } = new(
        null, RadioCalibrationState.NotStarted, [],
        "Turn on the transmitter, then start calibration and move every stick and switch to its extremes.",
        []);
}
