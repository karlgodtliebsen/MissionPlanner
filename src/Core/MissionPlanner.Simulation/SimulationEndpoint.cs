namespace MissionPlanner.Simulation;

/// <summary>Describes one named simulator endpoint.</summary>
/// <param name="Name">Stable endpoint role, such as MAVLink or console.</param>
/// <param name="Transport">Endpoint transport.</param>
/// <param name="Host">Host or bind address.</param>
/// <param name="Port">IP port.</param>
public sealed record SimulationEndpoint(
    string Name,
    SimulationEndpointTransport Transport,
    string Host,
    int Port)
{
    /// <summary>Gets a user-facing endpoint description.</summary>
    public string DisplayText => $"{Name}: {Transport.ToString().ToLowerInvariant()}://{Host}:{Port}";
}
