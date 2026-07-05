namespace Domain.Library.EventHub.DisposableWrappers;

internal class EventSubscriptionWithActionDispose(EventHub instance, string @event, Action action) : IDisposable
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
