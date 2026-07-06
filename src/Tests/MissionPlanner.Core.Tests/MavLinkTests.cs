using System.Net;
using FluentAssertions;
using MissionPlanner.Core.Tests.Configuration;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Simulator;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests for the MAVLink client and transport implementations.
/// </summary>
public class MavLinkTests
{
    private readonly ITestOutputHelper output;

    private readonly ServiceProvider serviceProvider;
    //UDP transport
    //Receive loop
    //Events
    //Cancellation
    //Lifecycle

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public MavLinkTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// Tests that the MAVLink client can receive data from a fake vehicle.
    /// </summary>
    [Fact]
    public async Task Should_Receive_Data_From_Fake_Vehicle()
    {
        TaskCompletionSource<byte[]> received = new();
        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();

        client.DataReceived += (data, _) =>
        {
            received.TrySetResult(data.Data.ToArray());
            return Task.CompletedTask;
        };

        await client.StartAsync(TestContext.Current.CancellationToken);

        await using FakeMavLinkVehicle simulator = new("127.0.0.1", 14550);

        await simulator.StartAsync();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotEmpty(result);

        await client.StopAsync();
    }

    /// <summary>
    /// Tests that the MAVLink client can receive a valid MAVLink v2 heartbeat frame from a fake vehicle.
    /// </summary>
    [Fact]
    public async Task Should_Receive_Valid_MavLinkV2_Heartbeat_Frame()
    {
        TaskCompletionSource<byte[]> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();

        client.DataReceived += (data, _) =>
        {
            received.TrySetResult(data.Data.ToArray());
            return Task.CompletedTask;
        };

        await client.StartAsync(TestContext.Current.CancellationToken);

        await using FakeMavLinkVehicle2 simulator = new(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(), "127.0.0.1", 14550, 14551, TimeSpan.FromMilliseconds(100));

        await simulator.StartAsync(TestContext.Current.CancellationToken);

        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(0xFD, frame[0]); // MAVLink v2
        Assert.Equal(9, frame[1]); // HEARTBEAT payload length
        Assert.Equal(1, frame[5]); // SystemId
        Assert.Equal(1, frame[6]); // ComponentId
        Assert.Equal(0u, GetMessageId(frame));

        await client.StopAsync();
    }

    private static uint GetMessageId(byte[] frame)
    {
        return
            frame[7]
            | ((uint)frame[8] << 8)
            | ((uint)frame[9] << 16);
    }


    /// <summary>
    /// Tests that the MAVLink client can calculate a valid CRC for a known MAVLink v2 heartbeat frame.
    /// </summary>
    [Fact]
    public void Should_Have_Valid_Crc_For_Known_Heartbeat()
    {
        CommonMavLinkCrcExtraProvider provider = new();
        var frame = MavLinkKnownFrames.CreateHeartbeatV2(provider);

        var payloadLength = frame[1];
        var messageId =
            frame[7]
            | ((uint)frame[8] << 8)
            | ((uint)frame[9] << 16);


        Assert.True(provider.TryGetCrcExtra(messageId, out var crcExtra));

        var calculatedCrc = MavLinkCrc.Calculate(
            frame.AsSpan(1, 9 + payloadLength),
            crcExtra);

        var receivedCrcOffset = 10 + payloadLength;

        var receivedCrc =
            (ushort)(frame[receivedCrcOffset]
                     | (frame[receivedCrcOffset + 1] << 8));

        Assert.Equal(calculatedCrc, receivedCrc);
    }

    /// <summary>
    /// Tests that the MAVLink client can parse a valid MAVLink v2 heartbeat frame from a fake vehicle.
    /// </summary>
    [Fact]
    public async Task Should_Parse_Heartbeat_Frame_From_Fake_Vehicle()
    {
        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();

        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Add debug output
        output.WriteLine($"Client IsRunning: {client.IsRunning}");
        output.WriteLine($"Transport IsConnected: {client.IsConnected}");

        await using FakeMavLinkVehicle2 simulator = new(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(), "127.0.0.1", 14550, 14551, TimeSpan.FromMilliseconds(100));

        await simulator.StartAsync(TestContext.Current.CancellationToken);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        output.WriteLine($"Client still running: {client.IsRunning}");

        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MavLinkFrame? messageResult = null;
        using var subscription = eventHub.SubscribeAsync<MavLinkFrame>(MavLinkEventTopics.ReceivedFrame, (frame, cts) =>
        {
            if (frame.MessageId == MessageIds.Heartbeat == (messageResult is null))
            {
                messageResult = frame;
                ts.SetResult();
            }

            return Task.CompletedTask;
        });

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        messageResult.Should().NotBeNull();
        var frame = messageResult!;

        Assert.Equal(1, frame.SystemId);
        Assert.Equal(1, frame.ComponentId);
        Assert.Equal(0u, frame.MessageId);
        // Assert.Equal(0, frame.Sequence);
        Assert.Equal(0xFD, frame.RawBytes.Span[0]);
    }

    /// <summary>
    /// Tests that the MAVLink parser rejects a frame with an invalid CRC.
    /// </summary>
    [Fact]
    public void Should_Reject_Frame_With_Invalid_Crc()
    {
        MavLinkV2FrameParser parser = new(new CommonMavLinkCrcExtraProvider());

        CommonMavLinkCrcExtraProvider provider = new();
        var frame = MavLinkKnownFrames.CreateHeartbeatV2(provider);
        frame[^1] ^= 0xFF;
        TransportEndPoint remoteMavLink = new("udp", IPAddress.Loopback.ToString(), 14550);
        var frames = parser.Parse(frame, remoteMavLink, DateTimeOffset.UtcNow);

        Assert.Empty(frames);
    }
}


//DroneGcs.Core.Tests
//    ↓
//FakeMavLinkVehicle
//    ↓ UDP loopback
//DroneGcs.MavLink
