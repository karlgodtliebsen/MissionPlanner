using MissionPlanner.Simulation.ArduPilot;

namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Builds an ArduPilot direct-binary launch plan from typed profile values.</summary>
public interface IArduPilotLaunchPlanBuilder
{
    /// <summary>Builds a tokenized launch plan without invoking a shell.</summary>
    /// <param name="profile">Validated simulator profile.</param>
    /// <param name="workingDirectory">Absolute isolated session working directory.</param>
    /// <returns>The launch plan.</returns>
    ArduPilotLaunchPlan Build(SimulatorProfile profile, string workingDirectory);
}
