using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>
/// Parses stateful MAVLink byte streams into validated protocol frames.
/// </summary>
public interface IMavLinkFrameParser
{
    /// <summary>
    /// Parses the raw MAVLink data into a list of MAVLink frames.
    /// </summary>
    /// <param name="data">The raw MAVLink data to parse.</param>
    /// <param name="endpoint">The endpoint from which the data was received.</param>
    /// <param name="receivedAt">The timestamp when the data was received.</param>
    /// <returns>A list of parsed MAVLink frames.</returns>
    IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, TransportEndPoint endpoint, DateTimeOffset receivedAt);

    /// <summary>
    /// Discards any incomplete frame bytes retained from the previous transport stream.
    /// </summary>
    void Reset();
}
