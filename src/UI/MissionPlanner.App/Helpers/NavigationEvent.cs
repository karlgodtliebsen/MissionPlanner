namespace MissionPlanner.App.Helpers;

/// <summary>
/// Represents a navigation event with previous and current locations.
/// </summary>
/// <param name="Previous">The previous location.</param>
/// <param name="Current">The current location.</param>
public sealed record NavigationEvent(string? Previous, string? Current);
