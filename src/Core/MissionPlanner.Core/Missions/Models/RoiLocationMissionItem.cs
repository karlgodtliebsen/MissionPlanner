namespace MissionPlanner.Core.Missions.Models;

/// <summary>Represents a geographic camera/gimbal region-of-interest command.</summary>
/// <param name="Id">Unique mission-item identifier.</param>
/// <param name="Sequence">Mission sequence.</param>
/// <param name="Position">ROI location.</param>
/// <param name="Altitude">ROI altitude and reference.</param>
/// <param name="UseLegacyCommand">Whether a decoded compatible legacy DO_SET_ROI should be preserved.</param>
/// <param name="AutoContinue">Whether execution continues automatically.</param>
public sealed record RoiLocationMissionItem(MissionItemId Id, ushort Sequence, GeoPosition Position,
    MissionAltitude Altitude, bool UseLegacyCommand = false, bool AutoContinue = true) : MissionItem(Id, Sequence, AutoContinue)
{
    /// <inheritdoc />
    public override MissionCommand Command => UseLegacyCommand ? MissionCommand.SetRoi : MissionCommand.SetRoiLocation;
    /// <inheritdoc />
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
