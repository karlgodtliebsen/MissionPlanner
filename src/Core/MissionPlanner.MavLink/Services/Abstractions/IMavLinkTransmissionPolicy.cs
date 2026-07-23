namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>Guards outbound MAVLink transmission at the shared connection boundary.</summary>
public interface IMavLinkTransmissionPolicy
{
    /// <summary>Throws when the current application mode prohibits outbound transmission.</summary>
    void ThrowIfTransmissionProhibited();
}

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
