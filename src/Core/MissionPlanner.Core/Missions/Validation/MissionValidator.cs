using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Validation;

/// <summary>
/// Provides the public API for MissionValidator.
/// </summary>
public sealed class MissionValidator : IMissionValidator
{
    /// <summary>
    /// Provides the public API for Validate.
    /// </summary>
    public MissionValidationResult Validate(Mission mission)
    {
        var issues = new List<MissionValidationIssue>();
        if (mission.Items.Count == 0)
        {
            issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, null, "mission.empty", "The mission contains no executable items."));
        }

        for (var i = 0; i < mission.Items.Count; i++)
        {
            var item = mission.Items[i];
            if (item.Sequence != i)
            {
                issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, item.Id, "mission.sequence", $"Expected sequence {i}, found {item.Sequence}."));
            }

            switch (item)
            {
                case WaypointMissionItem w
                    when !w.Position.IsValid:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, w.Id, "waypoint.position", "Waypoint coordinates are invalid."));
                    break;
                case TakeoffMissionItem t
                    when t.Altitude.Meters <= 0:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, t.Id, "takeoff.altitude", "Takeoff altitude must be above zero."));
                    break;
                case LandMissionItem l
                    when !l.Position.IsValid:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, l.Id, "land.position", "Landing coordinates are invalid."));
                    break;
                case ChangeSpeedMissionItem s
                    when s.SpeedMetersPerSecond <= 0:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, s.Id, "speed.value", "Speed must be above zero."));
                    break;
                case LoiterMissionItem o
                    when !o.Position.IsValid:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, o.Id, "loiter.position", "Loiter coordinates are invalid."));
                    break;
                case SplineWaypointMissionItem spline when !spline.Position.IsValid:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, spline.Id, "spline.position", "Spline waypoint coordinates are invalid."));
                    break;
                case RoiLocationMissionItem roi when !roi.Position.IsValid:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, roi.Id, "roi.position", "ROI coordinates are invalid."));
                    break;
                case JumpMissionItem jump when jump.TargetSequence >= mission.Items.Count:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, jump.Id, "jump.target", "DO_JUMP target does not exist after mission editing."));
                    break;
                case JumpMissionItem jump when jump.TargetSequence == jump.Sequence:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, jump.Id, "jump.self", "DO_JUMP cannot target itself."));
                    break;
                case JumpMissionItem jump when jump.RepeatCount < -1:
                    issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, jump.Id, "jump.repeat", "DO_JUMP repeat count must be -1, zero, or positive."));
                    break;
            }
        }

        if (mission.Items.Count(item => item is JumpMissionItem) > JumpMissionItem.ArduPilotCommandLimit)
            issues.Add(new MissionValidationIssue(MissionValidationSeverity.Error, null, "jump.limit", $"ArduPilot supports at most {JumpMissionItem.ArduPilotCommandLimit} DO_JUMP commands."));

        return new MissionValidationResult(issues);
    }
}
