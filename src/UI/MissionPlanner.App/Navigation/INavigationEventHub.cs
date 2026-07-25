using MissionPlanner.App.Helpers;
using MissionPlanner.App.Helpers.Navigation;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Defines the contract for an event hub that allows subscribing to and publishing events. 
/// </summary>
public interface INavigationEventHub
{
    /// <summary>
    /// Subscribes to a specific event with a synchronous action that accepts data.
    /// </summary>
    /// <param name="action">The action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>   
    IDisposable Subscribe(Action<NavigatedEvent> action);

    /// <summary>
    /// Subscribes to a specific event with a synchronous action that accepts data.
    /// </summary>
    /// <param name="action">The action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>
    IDisposable Subscribe(Action<NavigatingEvent> action);

    /// <summary>
    /// Publishes an event with data.
    /// </summary>
    /// <param name="data">The data to publish with the event.</param>
    void Publish(NavigatedEvent data);

    /// <summary>
    /// Publishes an event with data.
    /// </summary>
    /// <param name="data">The data to publish with the event.</param>
    void Publish(NavigatingEvent data);
}
