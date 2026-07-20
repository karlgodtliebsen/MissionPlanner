using FluentAssertions;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Simulator;
using MissionPlanner.Test.Support.Configuration;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests for the <see cref="MavLinkMessageDecoderHandler"/> class.
/// </summary>
public sealed class MavLinkMessageDecoderHandlerTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Provides the public API for MavLinkMessageDecoderHandlerTests.
    /// </summary>
    public MavLinkMessageDecoderHandlerTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// Tests that the <see cref="MavLinkMessageDecoderHandler"/> can decode a heartbeat message from a fake vehicle.
    /// </summary>
    [Fact]
    public async Task Should_Decode_Heartbeat_Message_From_Fake_Vehicle()
    {
        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await using FakeMavLinkVehicle2 simulator = new(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
            "127.0.0.1", 14550, 14551, TimeSpan.FromMilliseconds(100));

        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HeartbeatMessage? messageResult = null;

        using var subscription = eventHub.SubscribeAsync<HeartbeatMessage>(MavLinkEventTopics.NewMessage, (heartbeatMessage, ct) =>
        {
            messageResult = heartbeatMessage;
            ts.TrySetResult();
            return Task.CompletedTask;
        });

        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => simulator.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);


        await ts.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        messageResult.Should().NotBeNull();
        var message = messageResult!;

        Assert.Equal(1, message.SystemId);
        Assert.Equal(1, message.ComponentId);
        Assert.Equal(0u, message.CustomMode);
        Assert.Equal(2, message.VehicleType);
        Assert.Equal(3, message.Autopilot);
        Assert.Equal(0, message.BaseMode);
        Assert.Equal(4, message.SystemStatus);
        Assert.Equal(3, message.MavLinkVersion);
    }
}
