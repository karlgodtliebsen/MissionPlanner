using System.Net;

using Domain.Library.Configuration;
using Domain.Library.EventHub;
using Domain.Library.EventHub.Events;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Test.LibraryTests;

namespace MissionPlanner.Test.DomainEventsTests;

/// <summary>
/// TestOfEventHub
/// </summary>
public class TestOfDomainEventHub
{
    private readonly IServiceProvider serviceProvider;
    private readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

    /// <summary>
    /// 
    /// </summary>
    public TestOfDomainEventHub()
    {
        ILogger<EventHub> logger = NSubstitute.Substitute.For<ILogger<EventHub>>();
        IServiceCollection services = new ServiceCollection();
        services.AddEventHubServices();
        services.TryAddSingleton<IDomainEventHub, DomainEventHub>();
        services.AddSingleton<ILogger<EventHub>>(logger);
        serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_DomainEvent_Async_Publish_And_Subscription_Is_Connected()
    {
        var messageReceived = new TaskCompletionSource<bool>();

        IDomainEventHub eventHub = serviceProvider.GetRequiredService<IDomainEventHub>();
        var eventData = new EventData { Message = "hello universe" };
        var metaData = new MetaData { Actor = "Karl", Source = "From Application" };
        var domainEvent = new DomainEvent<EventData, MetaData>("testing", eventData, metaData);

        IDisposable disposable = eventHub.SubscribeDomainEventAsync<DomainEvent<EventData, MetaData>>((m, ct) =>
        {
            messageReceived.SetResult(true);
            return Task.CompletedTask;
        });


        await eventHub.PublishDomainEventAsync(domainEvent, cancellationToken);

        await Task.WhenAny(messageReceived.Task, Task.Delay(TimeSpan.FromMinutes(1), cancellationToken));
        messageReceived.Task.IsCompleted.Should().BeTrue("DomainEvent was received in the expected time frame.");
        disposable.Dispose();
    }


    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_DomainEvent_Publish_And_Subscription_Is_Connected()
    {
        var messageReceived = new TaskCompletionSource<bool>();

        IDomainEventHub eventHub = serviceProvider.GetRequiredService<IDomainEventHub>();
        var eventData = new EventData { Message = "hello universe" };
        var metaData = new MetaData { Actor = "Karl", Source = "From Application" };
        var domainEvent = new DomainEvent<EventData, MetaData>("testing", eventData, metaData);

        IDisposable disposable = eventHub.SubscribeDomainEvent<DomainEvent<EventData, MetaData>>((m) =>
        {
            messageReceived.SetResult(true);
            return;
        });


        eventHub.PublishDomainEvent(domainEvent);

        Task.WhenAny(messageReceived.Task, Task.Delay(TimeSpan.FromMinutes(1), cancellationToken));
        messageReceived.Task.IsCompleted.Should().BeTrue("DomainEvent was received in the expected time frame.");
        disposable.Dispose();
    }

    /// <summary>
    /// Verifies that a VehicleRegistered event is published when the first heartbeat is received.
    /// </summary>
    [Fact]
    public void Should_Publish_VehicleRegistered_When_First_Heartbeat_Is_Received()
    {
        var eventHub = new CapturingDomainEventHub(NSubstitute.Substitute.For<ILogger<EventHub>>());
        var registry = new VehicleRegistry(eventHub, NSubstitute.Substitute.For<ILogger<VehicleRegistry>>());

        registry.RegisterOrUpdateHeartbeat(
            new VehicleId(1, 1),
            new IPEndPoint(IPAddress.Any, 0),
            0,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        Assert.Contains(eventHub.Events, e => e is VehicleRegistered registered && registered.VehicleId == new VehicleId(1, 1));
    }
}