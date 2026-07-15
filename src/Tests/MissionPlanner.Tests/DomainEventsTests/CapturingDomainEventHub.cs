using Microsoft.Extensions.Logging;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Tests.DomainEventsTests;

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
    public override Task PublishDomainEventAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
    {
        events.Add(domainEvent);
        return base.PublishDomainEventAsync(domainEvent, cancellationToken);
    }
}
