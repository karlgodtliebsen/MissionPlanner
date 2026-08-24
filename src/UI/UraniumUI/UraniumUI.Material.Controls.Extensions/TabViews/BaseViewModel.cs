using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace UraniumUI.Material.TabViews;

public partial class BaseViewModel(ILogger logger) : ObservableObject, IDisposable, IActivationLifeCycle
{
    private readonly SemaphoreSlim operationGate = new(1, 1);

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
    /// Runs the specified operation asynchronously, handling busy state and exceptions.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    protected virtual async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (!await operationGate.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await operation(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Operation failed.");
            StatusMessage = $"Operation failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
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
