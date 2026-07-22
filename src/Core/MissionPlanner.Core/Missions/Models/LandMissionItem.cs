namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for LandMissionItem.
/// </summary>
/// <param name="Id">The stable mission item identifier.</param>
/// <param name="Sequence">The mission sequence number.</param>
/// <param name="Position">The Position value.</param>
/// <param name="Altitude">The Altitude value.</param>
/// <param name="AbortAltitudeMeters">The AbortAltitudeMeters value.</param>
/// <param name="DesiredYawDegrees">The DesiredYawDegrees value.</param>
/// <param name="AutoContinue">Whether execution advances automatically.</param>
public sealed record LandMissionItem(
    MissionItemId Id,
    ushort Sequence,
    GeoPosition Position,
    MissionAltitude Altitude,
    double? AbortAltitudeMeters = null,
    double? DesiredYawDegrees = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    /// <summary>
    /// Provides the public API for Command.
    /// </summary>
    public override MissionCommand Command => MissionCommand.Land;
    /// <summary>
    /// Provides the public API for Frame.
    /// </summary>
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
