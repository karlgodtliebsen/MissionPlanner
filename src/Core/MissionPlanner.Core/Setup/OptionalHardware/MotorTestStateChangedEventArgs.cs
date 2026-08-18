namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Provides an actuator-test state transition to observers.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class MotorTestStateChangedEventArgs(MotorTestSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new actuator-test state.</summary>
    public MotorTestSnapshot Snapshot { get; } = snapshot;
}
