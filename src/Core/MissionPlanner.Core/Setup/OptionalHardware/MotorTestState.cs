namespace MissionPlanner.Core.Setup.OptionalHardware;

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
