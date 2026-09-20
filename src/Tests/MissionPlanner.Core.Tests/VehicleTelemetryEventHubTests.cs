using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies isolated, nonblocking diagnostic event delivery.</summary>
public sealed class VehicleTelemetryEventHubTests
{
    /// <summary>Separate registrations retain independent subscriptions and vehicle identities.</summary>
    [Fact]
    public async Task DedicatedHubIsSeparateAndPreservesIdentity()
    {
        using var services = new ServiceCollection().AddLogging().AddEventHubServices()
            .AddDomainServices(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var hub = services.GetRequiredService<IVehicleTelemetryEventHub>();
        Assert.NotSame(services.GetRequiredService<IEventHub>(), hub);
        Assert.Same(hub, services.GetRequiredService<IVehicleTelemetryEventHub>());
        await hub.PublishAsync(new Sample(2, 0), TestContext.Current.CancellationToken);
        var received = new TaskCompletionSource<Sample>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = hub.SubscribeAsync<Sample>((sample, _) =>
        {
            received.TrySetResult(sample);
            return Task.CompletedTask;
        });
        await hub.PublishAsync(new Sample(42, 9), TestContext.Current.CancellationToken);
        Assert.Equal(new Sample(42, 9), await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
    }

    /// <summary>A blocked consumer cannot stall publishers or other consumers; its backlog stays bounded.</summary>
    [Fact]
    public async Task SlowAndFaultingSubscribersAreIsolatedAndDisposalCancels()
    {
        using var services = new ServiceCollection().AddLogging().AddEventHubServices()
            .AddDomainServices(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var hub = services.GetRequiredService<IVehicleTelemetryEventHub>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var bad = hub.SubscribeAsync<Sample>((_, _) => throw new InvalidOperationException("test subscriber"));
        var slow = hub.SubscribeAsync<Sample>(async (_, token) =>
        {
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            finally
            {
                stopped.TrySetResult();
            }
        });
        await hub.PublishAsync(new Sample(3, 0), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(hub.PublishAsync(new Sample(3, i), TestContext.Current.CancellationToken).IsCompletedSuccessfully);
        }
        Assert.True(hub.DroppedSamples >= 744);
        slow.Dispose();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var good = hub.SubscribeAsync<Sample>((_, _) =>
        {
            delivered.TrySetResult();
            return Task.CompletedTask;
        });
        await hub.PublishAsync(new Sample(7, 1), TestContext.Current.CancellationToken);
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
    }

    private sealed record Sample(int Vehicle, int Value);
}
