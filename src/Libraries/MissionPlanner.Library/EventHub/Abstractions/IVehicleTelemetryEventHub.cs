namespace MissionPlanner.Library.EventHub.Abstractions;

/// <summary>Dedicated, bounded diagnostic delivery independent of the application event hub.</summary>
public interface IVehicleTelemetryEventHub
{
    /// <summary>Queues an event without awaiting diagnostic consumers. Overflow drops oldest pending samples.</summary>
    Task PublishAsync<T>(T data, CancellationToken cancellationToken = default);

    /// <summary>Subscribes with an ordered, isolated queue. Disposal cancels delivery and removes pending work.</summary>
    IDisposable SubscribeAsync<T>(Func<T, CancellationToken, Task> handler);

    /// <summary>Gets samples dropped because a diagnostic consumer could not keep up.</summary>
    long DroppedSamples { get; }
}
