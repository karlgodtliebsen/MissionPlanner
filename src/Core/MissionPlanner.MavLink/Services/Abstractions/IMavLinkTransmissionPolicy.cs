namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>Guards outbound MAVLink transmission at the shared connection boundary.</summary>
public interface IMavLinkTransmissionPolicy
{
    /// <summary>Throws when the current application mode prohibits outbound transmission.</summary>
    void ThrowIfTransmissionProhibited();
}
