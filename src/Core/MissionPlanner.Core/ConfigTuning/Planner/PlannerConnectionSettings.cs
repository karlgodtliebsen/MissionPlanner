namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures connection defaults without storing credentials.</summary>
public sealed record PlannerConnectionSettings
{
    /// <summary>Gets the default connection channel.</summary>
    public string Channel { get; init; } = "AUTO";

    /// <summary>Gets the default network host.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Gets the default network port.</summary>
    public int Port { get; init; } = 14550;

    /// <summary>Gets the default serial baud rate.</summary>
    public int BaudRate { get; init; } = 115200;
}
