using Mapsui.Layers;

namespace MissionPlanner.App.Maps;

/// <summary>Contains a created layer or a typed ordinary failure.</summary>
/// <param name="Status">Creation outcome.</param>
/// <param name="Layer">Created layer on success.</param>
/// <param name="Message">Presentation-safe detail.</param>
public sealed record MapBasemapCreationResult(MapBasemapCreationStatus Status, ILayer? Layer, string? Message = null)
{
    /// <summary>Gets whether a usable layer was created.</summary>
    public bool IsSuccess => Status == MapBasemapCreationStatus.Success && Layer is not null;
}
