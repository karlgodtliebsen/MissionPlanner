namespace MissionPlanner.Transport;

/// <summary>
/// Represents the result of a transport receive operation.
/// </summary>
/// <param name="BytesRead">The number of bytes read from the transport.</param>
/// <param name="RemoteEndpoint">The remote endpoint from which the data was received.</param>
public readonly record struct TransportReceiveResult(int BytesRead, MavLinkEndpoint? RemoteEndpoint);