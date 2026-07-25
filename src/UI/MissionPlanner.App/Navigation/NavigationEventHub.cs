using Microsoft.Extensions.Logging;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Helpers.Navigation;
using MissionPlanner.Library.EventHub;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a hub for publishing and subscribing to domain events.
/// </summary>
/// <param name="logger">The logger instance.</param>
public class NavigationEventHub(ILogger<EventHub> logger) : EventHub(logger), INavigationEventHub
{
    /// <inheritdoc />
    public IDisposable Subscribe(Action<NavigatedEvent> action)
    {
        return base.Subscribe<NavigatedEvent>("ShellNavigated", action);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<NavigatingEvent> action)
    {
        return base.Subscribe<NavigatingEvent>("ShellNavigating", action);
    }

    /// <inheritdoc />
    public void Publish(NavigatedEvent data)
    {
        base.Publish<NavigatedEvent>("ShellNavigated", data);
    }

    /// <inheritdoc />
    public void Publish(NavigatingEvent data)
    {
        base.Publish<NavigatingEvent>("ShellNavigating", data);
    }
}
