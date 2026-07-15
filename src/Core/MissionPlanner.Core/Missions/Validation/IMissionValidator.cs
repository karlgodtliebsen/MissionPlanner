namespace MissionPlanner.Core.Missions.Validation;

public interface IMissionValidator
{
    MissionValidationResult Validate(Mission mission);
}
