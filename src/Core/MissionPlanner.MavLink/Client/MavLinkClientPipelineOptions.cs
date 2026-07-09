namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Options for configuring the MAVLink client pipeline.
/// </summary>
public sealed class MavLinkClientPipelineOptions
{
    /// <summary>Size of each rented receive buffer.</summary>
    public int ReceiveBufferSize { get; init; } = 4096;

    /// <summary>
    /// Maximum number of received byte blocks waiting for the MAVLink connection/parser.
    /// This should normally be large enough to absorb short event/publish stalls.
    /// </summary>
    public int ReceiveChannelCapacity { get; init; } = 512;

    /// <summary>
    /// Validates the pipeline options.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void Validate()
    {
        if (ReceiveBufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize));
        }

        if (ReceiveChannelCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ReceiveChannelCapacity));
        }
    }
}
