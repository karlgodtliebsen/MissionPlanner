namespace MissionPlanner.Simulation;

/// <summary>Configures bounded simulation-control discovery, readback, and event retention.</summary>
public sealed class SimulationControlOptions
{
    /// <summary>Application configuration section.</summary>
    public const string SectionName = "SimulationControls";

    /// <summary>Gets or sets the parameter discovery wait in milliseconds.</summary>
    public int DiscoveryWaitMilliseconds { get; set; } = 500;

    /// <summary>Gets or sets the confirmed-readback timeout in seconds.</summary>
    public int ReadbackTimeoutSeconds { get; set; } = 3;

    /// <summary>Gets or sets the maximum retained scenario events.</summary>
    public int EventCapacity { get; set; } = 500;
}
