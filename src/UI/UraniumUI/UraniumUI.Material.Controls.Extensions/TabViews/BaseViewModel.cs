using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Represents the base view model with common functionality for handling busy state, status messages, and error messages.
/// </summary>
public partial class BaseViewModel : ObservableObject, IDisposable, IActivationLifeCycle
{
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    protected BaseViewModel(ILogger logger)
    {
        dispatcher = ServiceHelper.GetRequiredService<IDispatcher>();
        this.logger = logger;
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
    /// Gets the latest operation or validation status.</summary>
    /// 
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
    }


    /// <summary>
    /// Runs the specified operation asynchronously, handling busy state and exceptions.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    protected virtual async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (!await operationGate.WaitAsync(0))
        {
            return;
        }
        dispatcher.Dispatch(() => IsBusy = true);
        try
        {
            await operation(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            dispatcher.Dispatch(() => StatusMessage = "Operation cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Operation failed.");
            dispatcher.Dispatch(() =>
            {

                StatusMessage = $"Operation failed: {exception.Message}";
                ErrorMessage = exception.Message;
            });
        }
        finally
        {
            dispatcher.Dispatch(() => IsBusy = false);
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        operationGate.Dispose();
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

}
