namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a navigation event with previous and current locations.
/// </summary>
/// <param name="Previous">The previous location.</param>
/// <param name="Current">The current location.</param>
/// <param name="EventArgs">The event arguments.</param>
public sealed record NavigatedEvent(string? Previous, string? Current, ShellNavigatedEventArgs EventArgs);
