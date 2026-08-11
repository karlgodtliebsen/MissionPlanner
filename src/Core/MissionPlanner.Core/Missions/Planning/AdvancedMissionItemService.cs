using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Default advanced mission-item editor.</summary>
public sealed class AdvancedMissionItemService : IAdvancedMissionItemService
{
    /// <inheritdoc />
    public MissionMapCommandAvailability AddSplineWaypoint(Mission mission, GeoPosition position, MissionAltitude altitude)
    {
        if (!position.IsValid)
            return MissionMapCommandAvailability.Disabled("Select a valid map position first.");
        mission.Add(new SplineWaypointMissionItem(MissionItemId.New(), 0, position, altitude, TimeSpan.Zero));
        return MissionMapCommandAvailability.Enabled;
    }

    /// <inheritdoc />
    public MissionMapCommandAvailability AddJumpToStart(Mission mission, int repeatCount)
    {
        var target = mission.Items.FirstOrDefault(item => item is not JumpMissionItem);
        return target is null
            ? MissionMapCommandAvailability.Disabled("Add an executable mission item before DO_JUMP.")
            : AddJump(mission, target.Sequence, repeatCount);
    }

    /// <inheritdoc />
    public MissionMapCommandAvailability AddJump(Mission mission, ushort targetSequence, int repeatCount)
    {
        if (repeatCount < -1)
            return MissionMapCommandAvailability.Disabled("Repeat count must be -1, zero, or positive.");
        if (targetSequence >= mission.Items.Count)
            return MissionMapCommandAvailability.Disabled("The selected jump target does not exist.");
        if (mission.Items.Count(item => item is JumpMissionItem) >= JumpMissionItem.ArduPilotCommandLimit)
            return MissionMapCommandAvailability.Disabled($"ArduPilot supports at most {JumpMissionItem.ArduPilotCommandLimit} DO_JUMP commands.");

        mission.Add(new JumpMissionItem(MissionItemId.New(), 0, targetSequence, repeatCount));
        return MissionMapCommandAvailability.Enabled;
    }

    /// <inheritdoc />
    public MissionMapCommandAvailability AddRoiLocation(Mission mission, GeoPosition position, MissionAltitude altitude)
    {
        if (!position.IsValid)
            return MissionMapCommandAvailability.Disabled("Select a valid map position first.");
        mission.Add(new RoiLocationMissionItem(MissionItemId.New(), 0, position, altitude));
        return MissionMapCommandAvailability.Enabled;
    }
}
