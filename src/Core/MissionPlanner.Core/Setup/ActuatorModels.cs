using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Identifies how a motor-test throttle value is expressed.</summary>
public enum MotorThrottleType
{
    /// <summary>Throttle as a percentage from zero to one hundred.</summary>
    Percent,
    /// <summary>Throttle as an absolute PWM value in microseconds.</summary>
    Pwm
}

/// <summary>Identifies the state of the actuator-test workflow.</summary>
public enum MotorTestState
{
    /// <summary>No actuator test is running.</summary>
    Idle,
    /// <summary>A bounded actuator test is running on the vehicle.</summary>
    Running,
    /// <summary>The last actuator test stopped normally.</summary>
    Stopped,
    /// <summary>The last actuator test was rejected or failed.</summary>
    Failed,
    /// <summary>The vehicle disconnected during an actuator test.</summary>
    Disconnected
}

/// <summary>Describes a bounded motor-test request.</summary>
/// <param name="MotorIndex">The one-based motor index.</param>
/// <param name="ThrottleType">How the throttle value is expressed.</param>
/// <param name="ThrottleValue">The throttle value in percent or PWM.</param>
/// <param name="DurationSeconds">The bounded run duration in seconds.</param>
public sealed record MotorTestRequest(int MotorIndex, MotorThrottleType ThrottleType, double ThrottleValue, double DurationSeconds);

/// <summary>Records one audit entry for an actuator-test operation.</summary>
/// <param name="Timestamp">The time the entry was recorded.</param>
/// <param name="Description">The operation description.</param>
/// <param name="Outcome">The operation outcome.</param>
public sealed record ActuatorTestLogEntry(DateTimeOffset Timestamp, string Description, string Outcome);

/// <summary>Represents the immutable state projected by the actuator-test UI.</summary>
/// <param name="VehicleId">The target vehicle, when a run exists.</param>
/// <param name="State">The current workflow state.</param>
/// <param name="ActiveMotor">The motor currently under test, when running.</param>
/// <param name="Instruction">The primary user instruction.</param>
/// <param name="Log">The bounded audit log, newest last.</param>
/// <param name="FailureReason">The terminal failure or disconnect explanation.</param>
public sealed record MotorTestSnapshot(
    VehicleId? VehicleId,
    MotorTestState State,
    int? ActiveMotor,
    string Instruction,
    IReadOnlyList<ActuatorTestLogEntry> Log,
    string? FailureReason = null)
{
    /// <summary>Gets the initial actuator-test state.</summary>
    public static MotorTestSnapshot Initial { get; } = new(
        null, MotorTestState.Idle, null,
        "Remove all propellers and keep the vehicle disarmed before testing actuators.",
        []);
}

/// <summary>Provides an actuator-test state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class MotorTestStateChangedEventArgs(MotorTestSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new actuator-test state.</summary>
    public MotorTestSnapshot Snapshot { get; } = snapshot;
}

/// <summary>Represents the outcome of a motor-test request.</summary>
/// <param name="Success">Whether the vehicle accepted the bounded test.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record MotorTestResult(bool Success, string Message);

/// <summary>Describes whether and how ESC calibration applies to the connected vehicle.</summary>
/// <param name="Applicable">Whether the detected ESC protocol requires manual calibration.</param>
/// <param name="ProtocolName">The detected output protocol name.</param>
/// <param name="Explanation">A user-facing explanation of the calibration requirement.</param>
/// <param name="Steps">The guided calibration steps, empty when not applicable.</param>
public sealed record EscCalibrationGuidance(
    bool Applicable,
    string ProtocolName,
    string Explanation,
    IReadOnlyList<string> Steps);
