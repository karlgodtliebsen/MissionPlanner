using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Stores bounded latest-per-axis PID_TUNING response context without initiating autotune.</summary>
public sealed class ControlResponseMetricsService : IControlResponseMetricsService, IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<(VehicleId VehicleId, byte Axis), ControlResponseMetric> metrics = [];
    private readonly IDisposable subscription;
    private bool disposed;

    /// <summary>Initializes the read-only response collector.</summary>
    /// <param name="eventHub">The decoded MAVLink event hub.</param>
    public ControlResponseMetricsService(IEventHub eventHub)
    {
        subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, OnMessageAsync);
    }

    /// <inheritdoc />
    public event EventHandler<ControlResponseMetricChangedEventArgs>? Changed;

    /// <inheritdoc />
    public IReadOnlyList<ControlResponseMetric> GetMetrics(VehicleId vehicleId)
    {
        lock (sync)
        {
            return metrics.Values
                .Where(metric => metric.VehicleId == vehicleId)
                .OrderBy(metric => metric.Axis)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        subscription.Dispose();
        lock (sync)
        {
            metrics.Clear();
        }
    }

    private Task OnMessageAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (disposed || message is not PidTuningMessage tuning)
        {
            return Task.CompletedTask;
        }

        var metric = new ControlResponseMetric(
            new VehicleId(tuning.SystemId, tuning.ComponentId),
            tuning.Axis,
            tuning.Desired,
            tuning.Achieved,
            tuning.Desired - tuning.Achieved,
            tuning.Ff,
            tuning.P,
            tuning.I,
            tuning.D,
            tuning.ReceivedAt);
        lock (sync)
        {
            metrics[(metric.VehicleId, metric.Axis)] = metric;
        }

        Changed?.Invoke(this, new ControlResponseMetricChangedEventArgs(metric));
        return Task.CompletedTask;
    }
}
