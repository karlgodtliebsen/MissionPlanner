namespace MissionPlanner.Core.Simulation;

/// <summary>Maps a logical simulation control to one firmware parameter variant.</summary>
/// <param name="Name">Exact parameter name.</param>
/// <param name="ActiveValue">Fixed active value for a fault; <see langword="null"/> uses the requested value.</param>
/// <param name="ResetValue">Fixed safe reset value; <see langword="null"/> restores the captured original.</param>
public sealed record SimulationParameterBinding(
    string Name,
    double? ActiveValue = null,
    double? ResetValue = null);
