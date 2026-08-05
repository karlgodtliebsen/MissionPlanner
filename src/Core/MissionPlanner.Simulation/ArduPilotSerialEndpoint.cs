namespace MissionPlanner.Simulation;

/// <summary>Describes one additional typed ArduPilot serial endpoint.</summary>
/// <param name="Index">ArduPilot serial index from 1 through 9; serial zero is reserved for MissionPlanner MAVLink.</param>
/// <param name="Transport">Endpoint transport.</param>
/// <param name="Host">Destination IP address or DNS host.</param>
/// <param name="Port">Destination port.</param>
public sealed record ArduPilotSerialEndpoint(
    int Index,
    ArduPilotSerialTransport Transport,
    string Host,
    int Port);
