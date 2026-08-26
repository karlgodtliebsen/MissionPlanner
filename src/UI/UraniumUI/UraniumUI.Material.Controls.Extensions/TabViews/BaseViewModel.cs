using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Represents the base view model with common functionality for handling busy state, status messages, and error messages.
/// </summary>
public partial class BaseViewModel : ObservableObject, IDisposable, IActivationLifeCycle
{
    private readonly SemaphoreSlim operationGate = new(1, 1);
    //private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private bool disposed;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    protected BaseViewModel(ILogger logger)
    {
        dispatcher = ServiceHelper.GetRequiredService<IDispatcher>();
        this.logger = logger;
    }

    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress
    {
        get;
        set;
    }

    /// <summary>
    /// Gets whether an operation is running.
    /// </summary>
    [ObservableProperty]
    public virtual partial bool IsBusy
    {
        get; set;
    }

    /// <summary>
    /// 
    /// </summary>
    protected void SetBusy()
    {
        DispatchIfAlive(
            () =>
            {
                IsBusy = true;
                Task.Yield();
            });
    }
    /// <summary>
    /// 
    /// </summary>
    protected void ResetBusy()
    {
        DispatchIfAlive(() =>
        {
            IsBusy = false;
            Task.Yield();
        });
    }

    /// <summary>
    /// Gets the latest operation or validation status.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public virtual partial string? StatusMessage { get; set; } = null;


    /// <summary>
    /// Gets whether a status message is available.
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>
    /// Gets the latest error message, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; } = null;

    /// <summary>
    /// Gets whether an error message is available.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="statusMessage"></param>
    /// <param name="errorMessage"></param>
    protected virtual void SetMessages(string? statusMessage = null, string? errorMessage = null)
    {
        if (dispatcher.IsDispatchRequired)
        {
            dispatcher.Dispatch(() => SetMessages(statusMessage, errorMessage));
            return;
        }
        StatusMessage = statusMessage;
        ErrorMessage = errorMessage;
        Task.Yield();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ex"></param>
    protected virtual void SetMessages(Exception? ex)
    {
        if (dispatcher.IsDispatchRequired)
        {
            dispatcher.Dispatch(() => SetMessages(null, ex.Message));
            return;
        }
        string? eMsg = null;
        if (ex is not null)
        {
            ErrorMessage = ex.Message;
        }
        StatusMessage = null;
        ErrorMessage = eMsg;
        Task.Yield();
    }


    /// <summary>
    /// Runs the specified operation asynchronously, handling busy state and exceptions.
    /// </summary>
    /// <param name="lifetimeCancellation"> </param>
    /// <param name="operation">The operation to run.</param>
    protected virtual async Task RunAsync(CancellationToken lifetimeCancellation, Func<CancellationToken, Task> operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!await operationGate.WaitAsync(0, lifetimeCancellation))
        {
            return;
        }
        DispatchIfAlive(() => IsBusy = true);
        try
        {
            lifetimeCancellation.ThrowIfCancellationRequested();
            await operation(lifetimeCancellation);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            DispatchIfAlive(() => StatusMessage = "Operation cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Operation failed.");
            DispatchIfAlive(() =>
            {
                StatusMessage = $"Operation failed: {exception.Message}";
                ErrorMessage = exception.Message;
            });
        }
        finally
        {
            DispatchIfAlive(() => IsBusy = false);
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        //lifetimeCancellation.Cancel();
        // An in-flight operation can still be observing this source. It is intentionally
        // retained until the view model becomes unreachable rather than raced by disposal.
    }

    /// <inheritdoc />
    public virtual Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }

    private void DispatchIfAlive(Action action)
    {
        dispatcher.Dispatch(() =>
        {
            if (!disposed)
            {
                action();
            }
        });
    }

}
