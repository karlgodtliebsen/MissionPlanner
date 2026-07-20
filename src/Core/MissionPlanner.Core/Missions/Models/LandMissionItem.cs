namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for LandMissionItem.
/// </summary>
/// <param name="Position">The Position value.</param>
/// <param name="Altitude">The Altitude value.</param>
/// <param name="AbortAltitudeMeters">The AbortAltitudeMeters value.</param>
/// <param name="DesiredYawDegrees">The DesiredYawDegrees value.</param>
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
