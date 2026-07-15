namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents a takeoff mission item in a mission plan.
/// </summary>
/// <param name="Id">The unique identifier of the mission item.</param>
/// <param name="Sequence">The sequence number of the mission item.</param>
/// <param name="Position">The geographic position of the takeoff point.</param>
/// <param name="Altitude">The altitude of the takeoff point.</param>
/// <param name="PitchDegrees">The pitch angle in degrees for the takeoff.</param>
/// <param name="HeadingDegrees">The heading angle in degrees for the takeoff.</param>
/// <param name="AutoContinue">Indicates whether the mission item should automatically continue to the next item.</param>
public sealed record TakeoffMissionItem(
    MissionItemId Id,
    ushort Sequence,
    GeoPosition? Position,
    MissionAltitude Altitude,
    double? PitchDegrees = null,
    double? HeadingDegrees = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    /// <inheritdoc />
    public override MissionCommand Command => MissionCommand.Takeoff;

    /// <inheritdoc />
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
