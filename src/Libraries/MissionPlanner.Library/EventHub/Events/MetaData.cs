namespace MissionPlanner.Library.EventHub.Events;

/// <summary>
/// Provides the public API for MetaData.
/// </summary>
public class MetaData
{
    /// <summary>
    /// Provides the public API for Actor.
    /// </summary>
    public string Actor { get; set; } = null!;

    /// <summary>
    /// Provides the public API for Source.
    /// </summary>
    public string Source { get; set; } = null!;
}
