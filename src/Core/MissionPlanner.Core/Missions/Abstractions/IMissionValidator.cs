using MissionPlanner.Core.Missions.Validation;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>
/// Interface for validating missions.      
/// </summary>
public interface IMissionValidator
{
    /// <summary>
    /// Validates the given mission.
    /// </summary>
    /// <param name="mission">The mission to validate.</param>
    /// <returns>The result of the mission validation.</returns>
    MissionValidationResult Validate(Mission mission);
}
