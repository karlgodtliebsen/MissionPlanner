namespace MissionPlanner.Maps.Feed;

/// <summary>Contains the signed content of a reviewed pack feed.</summary>
/// <param name="SchemaVersion">Feed schema version.</param>
/// <param name="FeedVersion">Monotonic feed content version.</param>
/// <param name="PublishedAt">Feed publication timestamp.</param>
/// <param name="Entries">Reviewed pack artifacts.</param>
public sealed record MapPackFeedPayload(int SchemaVersion, string FeedVersion, DateTimeOffset PublishedAt, MapPackFeedEntry[] Entries);
