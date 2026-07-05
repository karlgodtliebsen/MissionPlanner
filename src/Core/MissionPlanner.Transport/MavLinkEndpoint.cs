namespace MissionPlanner.Transport;

/// <summary>
/// 
/// </summary>
/// <param name="TransportName"></param>
/// <param name="Address"></param>
/// <param name="Port"></param>
public sealed record MavLinkEndpoint(string TransportName, string Address, int? Port = null)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return Port is null
            ? $"{TransportName}:{Address}"
            : $"{TransportName}:{Address}:{Port}";
    }
}