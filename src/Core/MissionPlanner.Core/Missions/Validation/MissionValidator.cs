using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Validation;

public sealed class MissionValidator : IMissionValidator
{
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
            }
        }

        return new MissionValidationResult(issues);
    }
}
