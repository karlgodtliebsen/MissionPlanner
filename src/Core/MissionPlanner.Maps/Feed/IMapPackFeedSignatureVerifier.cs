namespace MissionPlanner.Maps.Feed;

/// <summary>Verifies a reviewed map-pack feed signature.</summary>
public interface IMapPackFeedSignatureVerifier
{
    /// <summary>Verifies canonical payload bytes against a detached signature.</summary>
    bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature);
}
