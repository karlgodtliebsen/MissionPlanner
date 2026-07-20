namespace MissionPlanner.Library.EventHub.Events;

/// <summary>
/// Provides the public API for Aggregate.
/// </summary>
public class Aggregate
{
    /// <summary>
    /// Provides the public API for Type.
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Provides the public API for Id.
    /// </summary>
    public string Id { get; set; } = null!;
}
