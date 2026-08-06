namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Provides documented simulation controls and start-location presets.</summary>
public interface ISimulationControlCatalog
{
    /// <summary>Gets all documented controls, including explicitly unavailable capability placeholders.</summary>
    IReadOnlyList<SimulationControlDescriptor> Controls { get; }

    /// <summary>Gets built-in typed start-location presets.</summary>
    IReadOnlyList<SimulationLocationPreset> Locations { get; }
}
