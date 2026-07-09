namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Options for configuring the MAVLink connection pipeline.
/// </summary>
public sealed class MavLinkConnectionPipelineOptions
{
    /// <summary>
    /// Gets or sets the capacity of the decoded message channel.
    /// </summary>
    public int DecodedMessageChannelCapacity { get; init; } = 2048;

    /// <summary>
    /// Validates the options, throwing an exception if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>  
    public void Validate()
    {
        if (DecodedMessageChannelCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DecodedMessageChannelCapacity));
        }
    }
}
