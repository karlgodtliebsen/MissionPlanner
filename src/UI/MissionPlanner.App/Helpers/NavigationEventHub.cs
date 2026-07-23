using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.Library.EventHub;

namespace MissionPlanner.App.Helpers;

/// <summary>
/// Represents a hub for publishing and subscribing to domain events.
/// </summary>
/// <param name="logger">The logger instance.</param>
public class NavigationEventHub(ILogger<EventHub> logger) : EventHub(logger), INavigationEventHub
{
    /// <inheritdoc />
    public IDisposable Subscribe(Action<NavigationEvent> action)
    {
        return base.Subscribe<NavigationEvent>("ShellNavigated", action);
    }

    /// <inheritdoc />
    public void Publish(NavigationEvent data)
    {
        base.Publish<NavigationEvent>("ShellNavigated", data);
    }
}
