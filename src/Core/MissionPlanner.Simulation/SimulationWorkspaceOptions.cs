namespace MissionPlanner.Core.Simulation;

/// <summary>Configures simulation workspace lifecycle limits.</summary>
public sealed class SimulationWorkspaceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Simulation";

    /// <summary>Gets or sets the maximum heartbeat wait in seconds.</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 20;

    /// <summary>Gets or sets the maximum graceful stop wait in seconds.</summary>
    public int StopTimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the number of recent output lines retained in memory.</summary>
    public int RecentOutputCapacity { get; set; } = 500;

    /// <summary>Gets or sets the root directory for per-session runtime logs.</summary>
    public string LogRootDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "MissionPlanner", "Simulation");
}
