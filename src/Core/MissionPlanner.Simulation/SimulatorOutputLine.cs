namespace MissionPlanner.Core.Simulation;

/// <summary>Contains one timestamped simulator output line.</summary>
/// <param name="Timestamp">Capture time.</param>
/// <param name="Stream">Output stream.</param>
/// <param name="Text">Line text.</param>
public sealed record SimulatorOutputLine(
    DateTimeOffset Timestamp,
    SimulatorOutputStream Stream,
    string Text);
