using Avalonia.Threading;

namespace MissionPlanner.App.Utilities.Dispatching;

/// <inheritdoc />
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaUiDispatcher"/> class.
    /// </summary>
    /// <param name="dispatcher">The Avalonia dispatcher to use for UI thread operations.</param>
    public AvaloniaUiDispatcher(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public bool CheckAccess()
    {
        return dispatcher.CheckAccess();
    }

    /// <inheritdoc />
    public async Task DispatchAsync(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }

    /// <inheritdoc />
    public void Dispatch(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    /// <inheritdoc />
    public async Task<T> DispatchAsync<T>(Func<T> action)
    {
        return dispatcher.CheckAccess() ? action() : await dispatcher.InvokeAsync(action);
    }

    /// <inheritdoc />
    public T Dispatch<T>(Func<T> action)
    {
        return dispatcher.CheckAccess() ? action() : dispatcher.Invoke(action);
    }

    /// <inheritdoc />

    public async Task DispatchAsync(Func<Task> action)
    {
        if (dispatcher.CheckAccess())
        {
            await action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }
    /// <inheritdoc />

    public async Task<T> DispatchAsync<T>(Func<Task<T>> action)
    {
        return dispatcher.CheckAccess() ? await action() : await dispatcher.InvokeAsync(action);
    }
}
