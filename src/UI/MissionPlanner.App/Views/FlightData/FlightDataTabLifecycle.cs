using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Composes lazy initialization, visibility, connection cancellation, and subscription disposal for a Flight Data tab.
/// </summary>
public sealed class FlightDataTabLifecycle : IFlightDataTabLifecycle, IAsyncDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly Func<CancellationToken, Task>? initializeAsync;
    private readonly Func<CancellationToken, Task<IDisposable?>>? startAsync;
    private readonly SemaphoreSlim transitionLock = new(1, 1);
    private readonly object sync = new();
    private CancellationTokenSource? workCancellation;
    private IDisposable? activeWork;
    private Task pendingTransition = Task.CompletedTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataTabLifecycle"/> class.
    /// </summary>
    /// <param name="key">The stable tab key.</param>
    /// <param name="activeVehicle">The shared active-vehicle context.</param>
    /// <param name="initializeAsync">Optional expensive work run once on the first online activation.</param>
    /// <param name="startAsync">Optional vehicle-bound work started for each active online connection.</param>
    public FlightDataTabLifecycle(
        string key,
        IActiveVehicleContext activeVehicle,
        Func<CancellationToken, Task>? initializeAsync = null,
        Func<CancellationToken, Task<IDisposable?>>? startAsync = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        this.activeVehicle = activeVehicle;
        this.initializeAsync = initializeAsync;
        this.startAsync = startAsync;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <inheritdoc />
    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await transitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsActive)
            {
                return;
            }

            IsActive = true;
            activeVehicle.Changed += OnActiveVehicleChanged;
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            transitionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeactivateAsync()
    {
        if (disposed)
        {
            return;
        }

        CancelWork();
        await transitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            activeVehicle.Changed -= OnActiveVehicleChanged;
            StopWork();
        }
        finally
        {
            transitionLock.Release();
        }
    }

    /// <summary>
    /// Waits until a connection-triggered activation transition has settled. This is useful for deterministic tests and shutdown.
    /// </summary>
    /// <returns>A task representing the pending transition.</returns>
    public Task WhenSettledAsync()
    {
        lock (sync)
        {
            return pendingTransition;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await DeactivateAsync().ConfigureAwait(false);
        disposed = true;
        transitionLock.Dispose();
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsActive || !activeVehicle.IsOnline)
        {
            return;
        }

        workCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            activeVehicle.ConnectionCancellationToken);

        if (!IsInitialized)
        {
            await (initializeAsync?.Invoke(workCancellation.Token) ?? Task.CompletedTask).ConfigureAwait(false);
            IsInitialized = true;
        }

        activeWork = await (startAsync?.Invoke(workCancellation.Token) ?? Task.FromResult<IDisposable?>(null)).ConfigureAwait(false);
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs e)
    {
        CancelWork();
        var transition = RestartAsync();
        lock (sync)
        {
            pendingTransition = transition;
        }
    }

    private async Task RestartAsync()
    {
        await transitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            StopWork();
            if (IsActive)
            {
                await StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            transitionLock.Release();
        }
    }

    private void CancelWork()
    {
        lock (sync)
        {
            workCancellation?.Cancel();
        }
    }

    private void StopWork()
    {
        lock (sync)
        {
            activeWork?.Dispose();
            activeWork = null;
            workCancellation?.Cancel();
            workCancellation?.Dispose();
            workCancellation = null;
        }
    }
}
