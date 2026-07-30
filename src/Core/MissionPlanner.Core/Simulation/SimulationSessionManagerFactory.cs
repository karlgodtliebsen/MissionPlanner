using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.Core.Simulation;

/// <summary>Creates independent single-session lifecycle coordinators for fleet members.</summary>
public sealed class SimulationSessionManagerFactory(
    ISimulatorProfileValidator profileValidator,
    ISimulatorRuntime runtime,
    IDateTimeProvider clock,
    IOptions<SimulationWorkspaceOptions> options,
    ILoggerFactory loggerFactory) : ISimulationSessionManagerFactory
{
    /// <inheritdoc />
    public ISimulationSessionManager Create() => new SimulationSessionManager(
        profileValidator,
        runtime,
        clock,
        options,
        loggerFactory.CreateLogger<SimulationSessionManager>());
}
