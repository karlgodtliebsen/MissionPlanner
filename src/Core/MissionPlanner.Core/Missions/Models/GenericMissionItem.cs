using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Preserves a protocol mission item whose command or frame has no typed domain model, so that downloading,
/// saving and re-uploading a plan never loses it.
/// </summary>
/// <param name="Id">Unique mission-item identifier.</param>
/// <param name="Sequence">Mission sequence.</param>
/// <param name="Raw">The original protocol item, kept verbatim.</param>
public sealed record GenericMissionItem(MissionItemId Id, ushort Sequence, MavLinkMissionItem Raw)
    : MissionItem(Id, Sequence, Raw.AutoContinue)
{
    /// <inheritdoc />
    public override MissionCommand Command => (MissionCommand)Raw.Command;

    /// <inheritdoc />
    public override MissionFrame Frame => (MissionFrame)Raw.Frame;
}
