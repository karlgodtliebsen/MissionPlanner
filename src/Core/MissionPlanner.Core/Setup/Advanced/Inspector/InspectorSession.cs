using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Setup.Advanced.Inspector;

/// <summary>Owns an observer lease on the existing vehicle connection without taking transport ownership.</summary>
public sealed class InspectorSession(IVehicleConnectionSession connection, InspectorAggregator aggregator)
{
    private MavLinkInspectionLease? lease;
    private CancellationTokenSource? cancellation;
    private Task reader = Task.CompletedTask;

    /// <summary>Gets whether the observer still collects traffic.</summary>
    public bool IsCollecting => lease is not null && !reader.IsCompleted;
    /// <summary>Gets queue and key-capacity omissions.</summary>
    public long Dropped => (lease?.Dropped ?? 0) + aggregator.Omitted;

    /// <summary>Starts one observer on the current connection. The caller serializes lifecycle operations.</summary>
    public void Start(CancellationToken token)
    {
        if (lease is not null)
        {
            return;
        }
        var tap = connection.Connection.Inspection ?? throw new NotSupportedException("The current connection does not expose a traffic inspection tap.");
        token.ThrowIfCancellationRequested();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        lease = tap.Subscribe();
        aggregator.Clear();
        reader = ReadAsync(lease, cancellation.Token);
    }

    /// <summary>Releases the lease immediately and awaits the owned reader, preserving shared services.</summary>
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        lease?.Dispose();
        try
        {
            await reader.ConfigureAwait(false);
        }
        finally
        {
            cancellation?.Dispose();
            cancellation = null;
            lease = null;
            aggregator.Clear();
        }
    }

    /// <summary>Gets a bounded filtered set of aggregate rows.</summary>
    public IReadOnlyList<InspectorRow> Rows(string? search) => aggregator.Rows(search);
    /// <summary>Gets one selected frame's details.</summary>
    public InspectorDetails? Details(InspectorKey key) => aggregator.Details(key);
    /// <summary>Gets the complete bounded export.</summary>
    public InspectorSnapshot Export() => aggregator.Export(lease?.Dropped ?? 0);
    /// <summary>Clears accumulated statistics at the current collection boundary.</summary>
    public void Clear() => aggregator.Clear();

    private async Task ReadAsync(MavLinkInspectionLease observer, CancellationToken token)
    {
        try
        {
            await foreach (var observation in observer.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                aggregator.Observe(observation);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
