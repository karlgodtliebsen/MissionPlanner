namespace MissionPlanner.Core.Simulation;

/// <summary>Defines one safe embedded mission item for scenario upload.</summary>
/// <param name="Frame">MAV_FRAME numeric value.</param>
/// <param name="Command">MAV_CMD numeric value.</param>
/// <param name="Current">Whether the item is the current mission item.</param>
/// <param name="AutoContinue">Whether execution automatically advances.</param>
/// <param name="Param1">Command parameter 1.</param>
/// <param name="Param2">Command parameter 2.</param>
/// <param name="Param3">Command parameter 3.</param>
/// <param name="Param4">Command parameter 4.</param>
/// <param name="X">Protocol X coordinate/value.</param>
/// <param name="Y">Protocol Y coordinate/value.</param>
/// <param name="Z">Protocol Z coordinate/value.</param>
public sealed record SimulationScenarioMissionItem(
    byte Frame,
    ushort Command,
    bool Current,
    bool AutoContinue,
    float Param1,
    float Param2,
    float Param3,
    float Param4,
    int X,
    int Y,
    float Z);
