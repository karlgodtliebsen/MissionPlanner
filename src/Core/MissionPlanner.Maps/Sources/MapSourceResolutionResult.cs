namespace MissionPlanner.Maps.Sources;

/// <summary>Returns either a resolved source or a typed configuration outcome.</summary>
/// <param name="Status">Resolution status.</param>
/// <param name="Source">Resolved source on success.</param>
/// <param name="Message">Presentation-safe status detail.</param>
public sealed record MapSourceResolutionResult(
    MapSourceResolutionStatus Status,
    ResolvedMapSource? Source,
    string? Message = null)
{
    /// <summary>Gets whether resolution succeeded.</summary>
    public bool IsSuccess => Status == MapSourceResolutionStatus.None && Source is not null;
}
