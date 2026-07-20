namespace MissionPlanner.MavLink.MavFtp;

public static class MavFtpSequence
{
    public static ushort FirstResponse(ushort requestSequence) => unchecked((ushort)(requestSequence + 1));
    public static ushort Next(ushort sequence) => unchecked((ushort)(sequence + 1));

    public static bool MatchesSingleResponse(ushort requestSequence, ushort responseSequence)
    {
        return responseSequence == requestSequence || responseSequence == FirstResponse(requestSequence);
    }
    public static bool IsInResponseWindow(ushort requestSequence, ushort responseSequence, ushort maximumResponses = 128)
    {
        var distance = unchecked((ushort)(responseSequence - requestSequence));
        return distance is > 0 && distance <= maximumResponses;
    }
}
