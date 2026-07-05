using System.Net;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// 
/// </summary>
public interface IMavLinkFrameParser
{
    /// <summary>
    /// Parses the raw MAVLink data into a list of MAVLink frames.
    /// </summary>
    /// <param name="data">The raw MAVLink data to parse.</param>
    /// <param name="remoteEndpoint">The remote endpoint from which the data was received.</param>
    /// <param name="receivedAt">The timestamp when the data was received.</param>
    /// <returns>A list of parsed MAVLink frames.</returns>
    IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, MavLinkEndpoint? remoteEndpoint, DateTimeOffset receivedAt);


    /// <summary>
    /// Parses the raw MAVLink data into a list of MAVLink frames.
    /// </summary>
    /// <param name="data">The raw MAVLink data to parse.</param>
    /// <param name="remoteEndpoint">The remote endpoint from which the data was received.</param>
    /// <param name="receivedAt">The timestamp when the data was received.</param>
    /// <returns>A list of parsed MAVLink frames.</returns>
    IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, IPEndPoint remoteEndpoint, DateTimeOffset receivedAt);
}