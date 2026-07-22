using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Identifies the active and terminal stages of onboard compass calibration.</summary>
public enum CompassCalibrationWorkflowState
{
    /// <summary>No calibration has started.</summary>
    NotStarted,
    /// <summary>The start command is awaiting the first protocol evidence.</summary>
    Preparing,
    /// <summary>The vehicle is sampling one or more compasses.</summary>
    Running,
    /// <summary>Calibration succeeded but the results require explicit acceptance before they persist.</summary>
    PendingAcceptance,
    /// <summary>All calibrated compasses were accepted or auto-saved.</summary>
    Success,
    /// <summary>At least one compass explicitly failed calibration.</summary>
    Failed,
    /// <summary>The user cancelled calibration.</summary>
    Cancelled,
    /// <summary>The vehicle disconnected during calibration.</summary>
    Disconnected
}

/// <summary>Represents the per-compass calibration status projected from MAG_CAL_STATUS.</summary>
public enum CompassCalibrationStatus
{
    /// <summary>The compass has not begun calibration.</summary>
    NotStarted,
    /// <summary>The compass is queued and waiting to start.</summary>
    WaitingToStart,
    /// <summary>The compass is actively sampling.</summary>
    Running,
    /// <summary>The compass reported success.</summary>
    Success,
    /// <summary>The compass reported a generic failure.</summary>
    Failed,
    /// <summary>The compass reported a bad-orientation failure.</summary>
    BadOrientation,
    /// <summary>The compass reported a bad-radius failure.</summary>
    BadRadius
}

/// <summary>Describes one discovered compass instance built from parameters and device IDs.</summary>
/// <param name="Index">The one-based compass slot index.</param>
/// <param name="DeviceId">The bus device identifier from COMPASS_DEV_IDn.</param>
/// <param name="Use">Whether the compass is enabled for navigation.</param>
/// <param name="External">Whether the compass is marked external, when the parameter exists.</param>
/// <param name="Orientation">The configured board-rotation enumeration value.</param>
/// <param name="OrientationName">The human-readable orientation name.</param>
/// <param name="Priority">The one-based priority position, or zero when unranked.</param>
/// <param name="MotorCompensationConfigured">Whether motor interference compensation is enabled globally.</param>
/// <param name="OffsetX">The stored X offset.</param>
/// <param name="OffsetY">The stored Y offset.</param>
/// <param name="OffsetZ">The stored Z offset.</param>
/// <param name="Healthy">Aggregate 3D-magnetometer health when reported by the vehicle.</param>
public sealed record CompassInstance(
    int Index,
    uint DeviceId,
    bool Use,
    bool? External,
    int Orientation,
    string OrientationName,
    int Priority,
    bool MotorCompensationConfigured,
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    bool? Healthy)
{
    /// <summary>Gets whether this compass is the highest-priority (primary) instance.</summary>
    public bool IsPrimary => Priority == 1;
}

/// <summary>Represents one selectable compass board-orientation option.</summary>
/// <param name="Value">The MAV_SENSOR_ORIENTATION enumeration value.</param>
/// <param name="Name">The human-readable orientation label.</param>
public sealed record CompassOrientationOption(int Value, string Name);

/// <summary>Describes a discovered configuration inconsistency for compass setup.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record CompassConfigurationIssue(CompassIssueSeverity Severity, string Message);

/// <summary>Represents the severity of a compass configuration issue.</summary>
public enum CompassIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,
    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning
}

/// <summary>Represents the immutable compass inventory projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the inventory belongs to.</param>
/// <param name="Compasses">The discovered compass instances in slot order.</param>
/// <param name="OrientationOptions">The orientation choices available for editing.</param>
/// <param name="Issues">The detected duplicate-identity or priority inconsistencies.</param>
public sealed record CompassInventory(
    VehicleId VehicleId,
    IReadOnlyList<CompassInstance> Compasses,
    IReadOnlyList<CompassOrientationOption> OrientationOptions,
    IReadOnlyList<CompassConfigurationIssue> Issues)
{
    /// <summary>Gets an empty inventory for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An inventory with no compasses.</returns>
    public static CompassInventory Empty(VehicleId vehicleId) => new(vehicleId, [], [], []);
}

/// <summary>Projects the live per-compass calibration progress.</summary>
/// <param name="CompassId">The zero-based compass identifier reported by the vehicle.</param>
/// <param name="Status">The projected calibration status.</param>
/// <param name="CompletionPercent">The completion percentage from zero to one hundred.</param>
/// <param name="Attempt">The attempt number reported by the vehicle.</param>
public sealed record CompassCalibrationProgress(int CompassId, CompassCalibrationStatus Status, int CompletionPercent, int Attempt);

/// <summary>Projects a completed per-compass calibration report.</summary>
/// <param name="CompassId">The zero-based compass identifier.</param>
/// <param name="Success">Whether the compass calibrated successfully.</param>
/// <param name="Autosaved">Whether the vehicle already saved the result.</param>
/// <param name="Fitness">The reported RMS milligauss residual (lower is better).</param>
/// <param name="OffsetX">The computed X offset.</param>
/// <param name="OffsetY">The computed Y offset.</param>
/// <param name="OffsetZ">The computed Z offset.</param>
/// <param name="OldOrientation">The orientation before calibration.</param>
/// <param name="NewOrientation">The orientation after calibration.</param>
/// <param name="OrientationConfidence">The reported orientation confidence.</param>
public sealed record CompassCalibrationReport(
    int CompassId,
    bool Success,
    bool Autosaved,
    double Fitness,
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    int OldOrientation,
    int NewOrientation,
    double OrientationConfidence);

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

/// <summary>Provides a compass calibration state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class CompassCalibrationStateChangedEventArgs(CompassCalibrationSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new compass calibration state.</summary>
    public CompassCalibrationSnapshot Snapshot { get; } = snapshot;
}
