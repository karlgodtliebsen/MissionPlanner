namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents a loiter mission item in a mission plan.
/// </summary>
/// <param name="Id">The unique identifier of the mission item.</param>
/// <param name="Sequence">The sequence number of the mission item.</param>
/// <param name="Position">The geographic position of the loiter point.</param>
/// <param name="Altitude">The altitude of the loiter point.</param>
/// <param name="Time">The duration of the loiter in time, if applicable.</param>
/// <param name="Turns">The number of turns to loiter, if applicable.</param>
/// <param name="RadiusMeters">The radius of the loiter circle in meters, if applicable.</param>
/// <param name="DesiredYawDegrees">The desired yaw angle in degrees, if applicable.</param>
/// <param name="AutoContinue">Indicates whether the mission item should automatically continue to the next item.</param>
public sealed record LoiterMissionItem(
    MissionItemId Id,
    ushort Sequence,
    GeoPosition Position,
    MissionAltitude Altitude,
    TimeSpan? Time = null,
    double? Turns = null,
    double? RadiusMeters = null,
    double? DesiredYawDegrees = null,
    bool AutoContinue = true) : MissionItem(Id, Sequence, AutoContinue)
{
    /// <inheritdoc />
    public override MissionCommand Command => Time is not null
        ? MissionCommand.LoiterTime
        : Turns is not null
            ? MissionCommand.LoiterTurns
            : MissionCommand.LoiterUnlimited;

    /// <inheritdoc />
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
