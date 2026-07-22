using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Identifies the active and terminal stages of RC radio calibration.</summary>
public enum RadioCalibrationState
{
    /// <summary>No calibration has started.</summary>
    NotStarted,
    /// <summary>Live stick movement is being captured for endpoint extremes.</summary>
    Capturing,
    /// <summary>Captured endpoints are ready for validation and review.</summary>
    Review,
    /// <summary>Captured endpoints are being written and confirmed.</summary>
    Writing,
    /// <summary>Endpoints were written and confirmed by readback.</summary>
    Success,
    /// <summary>Validation or the confirmed write failed.</summary>
    Failed,
    /// <summary>The user cancelled calibration.</summary>
    Cancelled,
    /// <summary>The vehicle disconnected during calibration.</summary>
    Disconnected
}

/// <summary>Represents the severity of an RC configuration issue.</summary>
public enum RadioIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,
    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning,
    /// <summary>A hazardous configuration that blocks a confirmed write.</summary>
    Hazard
}

/// <summary>Describes a discovered RC configuration or calibration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record RadioValidationIssue(RadioIssueSeverity Severity, string Message);

/// <summary>Projects the live state of one RC input channel.</summary>
/// <param name="Number">The one-based channel number.</param>
/// <param name="Pwm">The latest raw PWM value in microseconds.</param>
/// <param name="Normalized">The trim-centered normalized position from minus one to one.</param>
/// <param name="Minimum">The configured minimum endpoint.</param>
/// <param name="Maximum">The configured maximum endpoint.</param>
/// <param name="Trim">The configured trim (center) value.</param>
/// <param name="Reversed">Whether the channel is reversed.</param>
/// <param name="FunctionName">The mapped pilot function, when known.</param>
public sealed record RadioChannelInfo(
    int Number,
    int Pwm,
    double Normalized,
    int Minimum,
    int Maximum,
    int Trim,
    bool Reversed,
    string? FunctionName);

/// <summary>Represents the immutable live RC channel projection shown by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the projection belongs to.</param>
/// <param name="Channels">The live channels in ascending order.</param>
/// <param name="IsStale">Whether the RC telemetry is older than the freshness window.</param>
/// <param name="Issues">Static configuration issues detected from parameters.</param>
public sealed record RadioChannelsView(
    VehicleId VehicleId,
    IReadOnlyList<RadioChannelInfo> Channels,
    bool IsStale,
    IReadOnlyList<RadioValidationIssue> Issues)
{
    /// <summary>Gets an empty projection for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty projection.</returns>
    public static RadioChannelsView Empty(VehicleId vehicleId) => new(vehicleId, [], true, []);
}

/// <summary>Captures the observed endpoint extremes for one channel during calibration.</summary>
/// <param name="Number">The one-based channel number.</param>
/// <param name="Minimum">The lowest observed PWM.</param>
/// <param name="Maximum">The highest observed PWM.</param>
/// <param name="Current">The latest observed PWM.</param>
public sealed record RadioChannelCapture(int Number, int Minimum, int Maximum, int Current)
{
    /// <summary>Gets the captured travel range in microseconds.</summary>
    public int Range => Maximum - Minimum;
}

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

/// <summary>Provides a radio calibration state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class RadioCalibrationStateChangedEventArgs(RadioCalibrationSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new radio calibration state.</summary>
    public RadioCalibrationSnapshot Snapshot { get; } = snapshot;
}

/// <summary>Represents the outcome of a confirmed radio endpoint write.</summary>
/// <param name="Success">Whether all endpoints were confirmed by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record RadioWriteResult(bool Success, string Message);
