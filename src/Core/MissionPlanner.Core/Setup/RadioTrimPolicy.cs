namespace MissionPlanner.Core.Setup;

/// <summary>Defines how a fresh Review-stage RC value is interpreted as channel trim.</summary>
public enum RadioTrimPolicy
{
    /// <summary>The control must be returned near the center of its captured travel.</summary>
    Centered,

    /// <summary>A conventional throttle must be placed at its low endpoint.</summary>
    Low,

    /// <summary>The current control position is recorded without centered-axis validation.</summary>
    Current
}
