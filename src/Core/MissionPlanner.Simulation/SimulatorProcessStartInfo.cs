namespace MissionPlanner.Simulation;

/// <summary>Contains platform-neutral local process start settings.</summary>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="WorkingDirectory">Absolute isolated working directory.</param>
/// <param name="Arguments">Individual process argument tokens.</param>
/// <param name="Environment">Explicit environment additions.</param>
/// <param name="ShowConsoleWindow">Whether a visible desktop console is requested.</param>
public sealed record SimulatorProcessStartInfo(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    bool ShowConsoleWindow);
