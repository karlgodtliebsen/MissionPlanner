namespace MissionPlanner.MavLink.Services;

/// <summary>Signals that application safety policy prohibited an outbound MAVLink frame.</summary>
public sealed class MavLinkTransmissionProhibitedException : InvalidOperationException
{
    /// <summary>Initializes a transmission-prohibition failure.</summary>
    /// <param name="message">Actionable safety-policy detail.</param>
    public MavLinkTransmissionProhibitedException(string message)
        : base(message)
    {
    }
}
