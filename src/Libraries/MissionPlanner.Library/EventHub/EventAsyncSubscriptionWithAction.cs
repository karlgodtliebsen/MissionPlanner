namespace Domain.Library.EventHub;

internal class EventAsyncSubscriptionWithAction(EventHub instance, string @event, Func<CancellationToken, Task> action) : IDisposable
{
    private EventHub? eventHubInstance = instance;

    void IDisposable.Dispose()
    {
        eventHubInstance?.Unsubscribe(@event, action);
        eventHubInstance = null;
    }
}
