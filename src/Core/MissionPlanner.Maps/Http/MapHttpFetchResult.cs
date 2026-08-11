namespace MissionPlanner.Maps.Http;

/// <summary>Contains fetched bytes or a typed transport outcome.</summary>
/// <param name="Status">Fetch status.</param>
/// <param name="Content">Returned bytes on success.</param>
/// <param name="FromCache">Whether bytes came from the HTTP cache.</param>
/// <param name="Message">Presentation-safe detail.</param>
public sealed record MapHttpFetchResult(MapHttpFetchStatus Status, byte[]? Content, bool FromCache, string? Message = null)
{
    /// <summary>Gets whether bytes were returned.</summary>
    public bool IsSuccess => Status == MapHttpFetchStatus.Success && Content is not null;
}
