using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Setup.Advanced.Inspector;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class InspectorTests
{
    private static readonly TransportEndPoint endpoint = new("test");
    private static readonly MavLinkMessageDefinitionRegistry definitions = new();

    [Fact]
    public void KeySeparationRatesFilteringAndClearAreDeterministic()
    {
        var clock = new Clock();
        var aggregate = new InspectorAggregator(definitions, clock);
        var observation = Observation();
        for (var index = 0; index < 10; index++)
        {
            aggregate.Observe(observation);
        }
        aggregate.Observe(observation with { Direction = MavLinkTrafficDirection.Outbound });
        aggregate.Observe(observation with { Frame = observation.Frame with { SystemId = 2 } });
        aggregate.Observe(observation with { Frame = observation.Frame with { ComponentId = 2 } });
        aggregate.Observe(observation with { Frame = observation.Frame with { MessageId = 999999 } });
        Assert.Equal(5, aggregate.Rows().Count);
        var row = Assert.Single(aggregate.Rows("sys:1 comp:1 id:0 dir:inbound"));
        Assert.Equal(10, row.Count);
        Assert.Equal(2, row.MessagesPerSecond);
        Assert.Equal(10 * observation.Frame.RawBytes.Length, row.Bytes);
        Assert.Single(aggregate.Rows("Unknown"));
        Assert.Empty(aggregate.Rows("sys:abc"));
        Assert.Equal(5, aggregate.Rows().Count);
        clock.Advance(5);
        Assert.All(aggregate.Rows(), item => Assert.Equal(0, item.MessagesPerSecond));
        aggregate.Clear();
        Assert.Empty(aggregate.Rows());
    }

    [Fact]
    public void HighRateAndUnknownKeyFloodRetainBoundedLatestState()
    {
        var aggregate = new InspectorAggregator(definitions, new Clock());
        var observation = Observation();
        for (var index = 0; index < 100000; index++)
        {
            aggregate.Observe(observation);
        }
        Assert.Equal(100000, Assert.Single(aggregate.Rows()).Count);
        for (uint id = 1; id <= 1000; id++)
        {
            aggregate.Observe(observation with { Frame = observation.Frame with { MessageId = id } });
        }
        Assert.Equal(InspectorAggregator.MaximumKeys, aggregate.Rows().Count);
        Assert.Equal(489, aggregate.Omitted);
        Assert.Equal(InspectorAggregator.MaximumKeys, aggregate.Export(3).Messages.Count);
        Assert.Equal(492, aggregate.Export(3).Dropped);
    }

    [Fact]
    public void BoundedObserversDropNewestWithoutStealingAndDisposeReleasesQueues()
    {
        var tap = new MavLinkInspectionTap();
        using var first = tap.Subscribe(2);
        using var second = tap.Subscribe(4);
        for (var index = 0; index < 3; index++)
        {
            tap.Publish(Observation());
        }
        Assert.Equal(1, first.Dropped);
        Assert.Equal(0, second.Dropped);
        Assert.Equal(2, first.Reader.Count);
        Assert.Equal(3, second.Reader.Count);
        first.Dispose();
        Assert.Equal(0, first.Reader.Count);
        Assert.True(tap.HasObservers);
        second.Dispose();
        Assert.False(tap.HasObservers);
        for (var cycle = 0; cycle < 10; cycle++)
        {
            using var next = tap.Subscribe(1);
            tap.Publish(Observation());
            Assert.Equal(1, next.Reader.Count);
        }
        Assert.False(tap.HasObservers);
    }

    [Fact]
    public void UnknownDialectCandidatesRemainRejectedButRetainSignedRawBytesForInspection()
    {
        var bytes = SignedHeartbeat();
        bytes[7] = bytes[8] = bytes[9] = 0xFF;
        var parser = new MavLinkV2FrameParser(definitions);
        MavLinkFrame? candidate = null;
        parser.UnknownFrameObserved += frame => candidate = frame;
        Assert.Empty(parser.Parse(bytes, endpoint, DateTimeOffset.UtcNow));
        Assert.NotNull(candidate);
        Assert.Equal(bytes, candidate.RawBytes.ToArray());
        var aggregate = new InspectorAggregator(definitions, new Clock());
        aggregate.Observe(new(MavLinkTrafficDirection.Inbound, candidate, null, false));
        var details = Assert.Single(aggregate.Export(0).Messages);
        Assert.Contains("CRC unverified", details.Row.Verification);
        Assert.Contains("signature present", details.Row.Verification);
        Assert.Equal(Convert.ToHexString(bytes), details.RawHex);
    }

    [Fact]
    public async Task ExistingPipelineDeliversSameDecodedMessageAndExactSignedBytesToIndependentTap()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var bytes = SignedHeartbeat();
        var queue = Channel.CreateUnbounded<PooledMavLinkDataReceived>();
        var client = Substitute.For<IMavLinkClient>();
        client.ReceivedBytes.Returns(queue.Reader);
        var message = new HeartbeatMessage(1, 1, endpoint, 4, 2, 3, 0, 4, 3, DateTimeOffset.UtcNow);
        var decoder = Substitute.For<IMavLinkMessageDecodeHandler>();
        decoder.TryDecode(Arg.Any<MavLinkFrame>(), out Arg.Any<MavLinkMessage?>()).Returns(call =>
        {
            call[1] = message;
            return true;
        });
        var tap = new MavLinkInspectionTap();
        using var lease = tap.Subscribe();
        var hub = Substitute.For<IEventHub>();
        var delivered = new TaskCompletionSource<MavLinkMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.PublishAsync<MavLinkMessage>(Arg.Any<string>(), Arg.Any<MavLinkMessage>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                delivered.TrySetResult(call.Arg<MavLinkMessage>()!);
                return Task.CompletedTask;
            });
        await using var connection = new MavLinkConnection(client, new MavLinkV2FrameParser(definitions), decoder,
            hub, Options.Create(new MavLinkConnectionPipelineOptions()), NullLogger<MavLinkConnection>.Instance, inspection: tap);
        await connection.StartAsync(deadline.Token);
        var memory = MemoryPool<byte>.Shared.Rent(bytes.Length);
        bytes.CopyTo(memory.Memory.Span);
        await queue.Writer.WriteAsync(new(memory, bytes.Length, endpoint, DateTimeOffset.UtcNow), deadline.Token);
        var observation = await lease.Reader.ReadAsync(deadline.Token);
        Assert.Same(message, observation.Message);
        Assert.True(observation.CrcVerified);
        Assert.Equal(bytes, observation.Frame.RawBytes.ToArray());
        Assert.Same(message, await delivered.Task.WaitAsync(deadline.Token));
        await connection.SendRawAsync(bytes, endpoint, deadline.Token);
        var outbound = await lease.Reader.ReadAsync(deadline.Token);
        Assert.Equal(MavLinkTrafficDirection.Outbound, outbound.Direction);
        Assert.Equal(bytes, outbound.Frame.RawBytes.ToArray());
        queue.Writer.TryComplete();
        await connection.StopAsync();
        decoder.Received(1).TryDecode(Arg.Any<MavLinkFrame>(), out Arg.Any<MavLinkMessage?>());
        Assert.False(tap.HasObservers);
    }

    private static MavLinkInspectionObservation Observation()
    {
        var raw = SignedHeartbeat();
        var frame = Assert.Single(new MavLinkV2FrameParser(definitions).Parse(raw, endpoint, DateTimeOffset.UtcNow));
        return new(MavLinkTrafficDirection.Inbound, frame, null, true);
    }

    private static byte[] SignedHeartbeat()
    {
        var original = MavLinkKnownFrames.CreateHeartbeatV2(new CommonMavLinkCrcExtraProvider(definitions));
        var signed = new byte[original.Length + 13];
        original.CopyTo(signed, 0);
        signed[2] = 1;
        definitions.TryGet(0, out var definition);
        var crc = MavLinkCrc.Calculate(signed.AsSpan(1, 9 + signed[1]), definition!.CrcExtra);
        BinaryPrimitives.WriteUInt16LittleEndian(signed.AsSpan(10 + signed[1], 2), crc);
        signed.AsSpan(original.Length).Fill(42);
        return signed;
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(int seconds) => now += TimeSpan.FromSeconds(seconds);
    }
}
