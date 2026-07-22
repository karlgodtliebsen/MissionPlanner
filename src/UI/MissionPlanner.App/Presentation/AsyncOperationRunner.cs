using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Executes active-vehicle operations with consistent cancellation and presentation state.
/// </summary>
public partial class AsyncOperationRunner : ObservableObject
{
    private readonly IActiveVehicleContext activeVehicle;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncOperationRunner"/> class.
    /// </summary>
    /// <param name="activeVehicle">The active-vehicle connection context.</param>
    public AsyncOperationRunner(IActiveVehicleContext activeVehicle)
    {
        this.activeVehicle = activeVehicle;
    }

    /// <summary>
    /// Gets or sets the current operation presentation state.
    /// </summary>
    [ObservableProperty]
    public partial AsyncOperationState State { get; set; } = AsyncOperationState.Idle;

    /// <summary>
    /// Runs an operation against the vehicle that is active when execution starts.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="busyMessage">The progress message.</param>
    /// <param name="timeout">The optional operation timeout.</param>
    /// <param name="cancellationToken">A caller cancellation token.</param>
    /// <returns>The final presentation state.</returns>
    public async Task<AsyncOperationState> RunAsync(
        Func<VehicleId, CancellationToken, Task<AsyncOperationState>> operation,
        string? busyMessage = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var target = activeVehicle.VehicleId;
        if (target is null || !activeVehicle.IsOnline)
        {
            return State = AsyncOperationState.Disconnected();
        }

        State = AsyncOperationState.Busy(busyMessage);
        using var timeoutSource = timeout is null ? null : new CancellationTokenSource(timeout.Value);
        using var linked = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken, timeoutSource.Token);

        try
        {
            return State = await operation(target.Value, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            return State = AsyncOperationState.Timeout("The operation timed out.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                                                 (!activeVehicle.IsOnline || activeVehicle.VehicleId != target))
        {
            return State = AsyncOperationState.Disconnected();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return State = AsyncOperationState.Error(exception.Message, exception);
        }
    }
}
