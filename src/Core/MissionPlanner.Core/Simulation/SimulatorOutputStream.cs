namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a simulator output channel.</summary>
public enum SimulatorOutputStream
{
    /// <summary>Standard output from the runtime.</summary>
    StandardOutput,

    /// <summary>Standard error from the runtime.</summary>
    StandardError,

    /// <summary>A lifecycle message produced by the simulation coordinator.</summary>
    System
}
