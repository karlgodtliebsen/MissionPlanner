namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpSequence.
/// </summary>
public static class MavFtpSequence
{
    /// <summary>
    /// Provides the public API for FirstResponse.
    /// </summary>
    public static ushort FirstResponse(ushort requestSequence) => unchecked((ushort)(requestSequence + 1));
    /// <summary>
    /// Provides the public API for Next.
    /// </summary>
    public static ushort Next(ushort sequence) => unchecked((ushort)(sequence + 1));

    /// <summary>
    /// Provides the public API for MatchesSingleResponse.
    /// </summary>
    public static bool MatchesSingleResponse(ushort requestSequence, ushort responseSequence)
    {
        return responseSequence == requestSequence || responseSequence == FirstResponse(requestSequence);
    }
    /// <summary>
    /// Provides the public API for IsInResponseWindow.
    /// </summary>
    public static bool IsInResponseWindow(ushort requestSequence, ushort responseSequence, ushort maximumResponses = 128)
    {
        var distance = unchecked((ushort)(responseSequence - requestSequence));
        return distance is > 0 && distance <= maximumResponses;
    }
}
