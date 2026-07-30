namespace MissionPlanner.Core.Simulation;

/// <summary>Contains a tokenized ArduPilot process launch plan.</summary>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="WorkingDirectory">Isolated session working directory.</param>
/// <param name="Arguments">Individual argument tokens.</param>
/// <param name="Environment">Explicit process environment additions.</param>
/// <param name="ConnectionEndpoint">MissionPlanner MAVLink listening endpoint.</param>
/// <param name="ExpectedSystemId">Expected MAVLink system ID.</param>
/// <param name="ShowConsoleWindow">Whether a visible desktop console is requested.</param>
public sealed record ArduPilotLaunchPlan(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    SimulationEndpoint ConnectionEndpoint,
    byte ExpectedSystemId,
    bool ShowConsoleWindow);
