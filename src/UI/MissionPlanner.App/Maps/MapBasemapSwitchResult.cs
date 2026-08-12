namespace MissionPlanner.App.Maps;

/// <summary>Describes a basemap switch without using exceptions for expected failures.</summary>
/// <param name="Status">Switch outcome.</param>
/// <param name="Message">Optional diagnostic message.</param>
public sealed record MapBasemapSwitchResult(MapBasemapSwitchStatus Status, string? Message = null)
{
    /// <summary>Gets whether the requested source was committed.</summary>
    public bool IsSuccess => Status == MapBasemapSwitchStatus.Success;
}
