namespace MissionPlanner.Core.Missions.Models;

/// <summary>Represents an ArduPilot DO_JUMP mission command.</summary>
/// <param name="Id">Unique mission-item identifier.</param>
/// <param name="Sequence">Mission sequence.</param>
/// <param name="TargetSequence">Zero-based MAVLink sequence to jump to.</param>
/// <param name="RepeatCount">Repeat count, or -1 for an infinite jump.</param>
/// <param name="AutoContinue">Whether execution continues automatically.</param>
public sealed record JumpMissionItem(MissionItemId Id, ushort Sequence, ushort TargetSequence,
    int RepeatCount, bool AutoContinue = true) : MissionItem(Id, Sequence, AutoContinue)
{
    /// <summary>Maximum DO_JUMP commands supported by ArduPilot mission execution.</summary>
    public const int ArduPilotCommandLimit = 15;
    /// <inheritdoc />
    public override MissionCommand Command => MissionCommand.Jump;
    /// <inheritdoc />
    public override MissionFrame Frame => MissionFrame.Mission;
}
