namespace MissionPlanner.Maps.Http;

/// <summary>Metadata retained for a protocol-aware HTTP cache entry.</summary>
/// <param name="ExpiresAt">Optional expiry.</param>
/// <param name="EntityTag">Optional ETag.</param>
/// <param name="LastModified">Optional last-modified timestamp.</param>
public sealed record MapHttpCacheMetadata(DateTimeOffset? ExpiresAt, string? EntityTag, DateTimeOffset? LastModified);
