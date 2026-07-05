namespace Domain.Library.EventHub.DisposableWrappers;

internal class EventSubscriptionWithStringActionDispose(EventHub instance, Action<string> action) : IDisposable
{
    private EventHub? eventHubInstance = instance;

    void IDisposable.Dispose()
    {
        if (eventHubInstance is not null)
        {
            eventHubInstance.UnsubscribeFromAll(action);
            eventHubInstance = null;
        }
    }
}
