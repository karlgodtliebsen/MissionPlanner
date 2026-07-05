using Domain.Library.EventHub;
using Domain.Library.EventHub.Events;

using Microsoft.Extensions.Logging;

using MissionPlanner.Core.DomainEvents;

namespace MissionPlanner.Test.DomainEventsTests;

/// <summary>
/// 
/// </summary>
/// <param name="logger"></param>
public sealed class CapturingDomainEventHub(ILogger<EventHub> logger) : DomainEventHub(logger)
{
    private readonly List<IDomainEvent> events = [];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<IDomainEvent> Events => events;


    /// <inheritdoc />
    public override void PublishDomainEvent<T>(T domainEvent)
    {
        events.Add(domainEvent);
        base.PublishDomainEvent(domainEvent);
    }

    /// <inheritdoc />
    public override Task PublishDomainEventAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
    {
        events.Add(domainEvent);
        return base.PublishDomainEventAsync(domainEvent, cancellationToken);
    }
}