using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

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
