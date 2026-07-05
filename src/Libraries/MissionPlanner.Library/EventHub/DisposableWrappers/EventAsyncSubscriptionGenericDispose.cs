namespace Domain.Library.EventHub.DisposableWrappers;

internal class EventAsyncSubscriptionGenericDispose<T>(EventHub instance, string @event, Func<T, CancellationToken, Task> action) : IDisposable
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
