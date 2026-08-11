namespace MissionPlanner.Maps.Feed;

/// <summary>Wraps canonical feed content with its detached signature.</summary>
/// <param name="Payload">Signed feed content.</param>
/// <param name="Signature">Base64 signature over canonical UTF-8 payload JSON.</param>
public sealed record SignedMapPackFeed(MapPackFeedPayload Payload, string Signature);
