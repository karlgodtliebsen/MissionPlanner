namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for ChangeSpeedMissionItem.
/// </summary>
/// <param name="Id">The stable mission item identifier.</param>
/// <param name="Sequence">The mission sequence number.</param>
/// <param name="SpeedType">The SpeedType value.</param>
/// <param name="SpeedMetersPerSecond">The SpeedMetersPerSecond value.</param>
/// <param name="ThrottlePercent">The ThrottlePercent value.</param>
/// <param name="AutoContinue">Whether execution advances automatically.</param>
public sealed record ChangeSpeedMissionItem(
    MissionItemId Id,
    ushort Sequence,
    MissionSpeedType SpeedType,
    double SpeedMetersPerSecond,
    double? ThrottlePercent = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    /// <summary>
    /// Provides the public API for Command.
    /// </summary>
    public override MissionCommand Command => MissionCommand.ChangeSpeed;
    /// <summary>
    /// Provides the public API for Frame.
    /// </summary>
    public override MissionFrame Frame => MissionFrame.GlobalRelativeAltitude;
}
