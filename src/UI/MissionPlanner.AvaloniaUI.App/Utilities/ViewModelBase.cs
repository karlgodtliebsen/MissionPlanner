using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>
/// Base class for all view models, providing property change notification and disposal support.
/// </summary>
public partial class ViewModelBase : ObservableObject, IDisposable
{

    /// <summary>
    /// Gets or sets the notification manager for displaying window notifications.
    /// </summary>
    public WindowNotificationManager? NotificationManager
    {
        get; set;
    }
    /// <summary>
    /// Gets or sets the toast manager for displaying window toasts.
    /// </summary>
    public WindowToastManager? ToastManager
    {
        get; set;
    }


    [RelayCommand]
    private void ShowToast(string message)
    {
        ToastManager?.Show(message);
    }

    [RelayCommand]
    private void ShowNotification(string message)
    {
        NotificationManager?.Show(message);
    }


    private readonly SemaphoreSlim operationGate = new(1, 1);
    private bool disposed;
    private readonly IDomainEventHub eventHub;

    /// <summary>
    /// The logger instance for this view model.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The UI dispatcher instance for this view model.
    /// </summary>
    protected readonly IUiDispatcher Dispatcher;

    /// <inheritdoc />
    protected ViewModelBase(ILogger logger)
    {
        Logger = logger;
        Dispatcher = ServiceHelper.GetRequiredService<IUiDispatcher>();
        eventHub = ServiceHelper.GetRequiredService<IDomainEventHub>();
    }

    /// <inheritdoc />
    protected ViewModelBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<ViewModelBase>>();
        Dispatcher = ServiceHelper.GetRequiredService<IUiDispatcher>();
        eventHub = ServiceHelper.GetRequiredService<IDomainEventHub>();
    }

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
        get;
        set;
    }

    /// <summary>
    /// 
    /// </summary>
    protected void SetBusy()
    {
        DispatchIfAlive(() =>
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
    public virtual partial string? StatusMessage
    {
        get;
        set;
    }

    partial void OnStatusMessageChanged(string? value)
    {
        eventHub.PublishDomainEventAsync<StatusMessageReceived>(new StatusMessageReceived(value));
    }


    /// <summary>
    /// Gets whether a status message is available.
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>
    /// Gets the latest error message, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage
    {
        get;
        set;
    } = null;

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
        Dispatcher.Dispatch(() =>
        {
            StatusMessage = statusMessage;
            ErrorMessage = errorMessage;
        });

        Task.Yield();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ex"></param>
    protected virtual void SetMessages(Exception? ex)
    {
        string? eMsg = null;
        Dispatcher.Dispatch(() =>
        {
            if (ex is not null)
            {
                ErrorMessage = ex.Message;
            }

            StatusMessage = null;
            ErrorMessage = eMsg;
        });
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
            Logger.LogError(exception, "Operation failed.");
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
        Dispatcher.Dispatch(() =>
        {
            if (!disposed)
            {
                action();
            }
        });
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
}
