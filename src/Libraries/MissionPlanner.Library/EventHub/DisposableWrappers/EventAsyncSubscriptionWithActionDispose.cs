namespace Domain.Library.EventHub.DisposableWrappers;

internal class EventAsyncSubscriptionWithActionDispose(EventHub instance, string @event, Func<CancellationToken, Task> action) : IDisposable
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
