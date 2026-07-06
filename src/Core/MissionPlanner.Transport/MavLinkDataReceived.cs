namespace MissionPlanner.Transport;

/// <summary>
/// Represents data received from a MAVLink endpoint.
/// </summary>
/// <param name="Data">The received data.</param>
/// <param name="RemoteEndpoint">The endpoint from which the data was received.</param>
/// <param name="ReceivedAt">The timestamp when the data was received.</param>
public sealed record MavLinkDataReceived(ReadOnlyMemory<byte> Data, TransportEndPoint? RemoteEndpoint, DateTimeOffset ReceivedAt);
