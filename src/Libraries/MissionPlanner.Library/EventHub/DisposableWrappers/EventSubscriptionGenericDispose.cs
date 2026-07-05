namespace Domain.Library.EventHub.DisposableWrappers;

internal class EventSubscriptionGenericDispose<T>(EventHub instance, string @event, Action<T> action) : IDisposable
{
    private EventHub? eventHubInstance = instance;

    void IDisposable.Dispose()
    {
        if (eventHubInstance is not null)
        {
            eventHubInstance.Unsubscribe(@event, action);
            eventHubInstance = null;
        }
    }
}
