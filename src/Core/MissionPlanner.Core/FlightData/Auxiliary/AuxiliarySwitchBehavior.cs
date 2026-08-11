namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Describes how an auxiliary function consumes switch input.</summary>
public enum AuxiliarySwitchBehavior
{
    /// <summary>The function consumes low, middle, and high states.</summary>
    ThreePosition,

    /// <summary>The function represents a bounded press/release action.</summary>
    Momentary
}
