using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Replay;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Logging;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks classic binary format, bounded storage, and lifecycle isolation.</summary>
public sealed class TelemetryRecordingTests
{
    private static readonly byte[] V1 = [0xFE, 0, 0, 1, 1, 0, 0, 0];
    private static readonly DateTimeOffset Received = DateTimeOffset.Parse("2026-09-16T12:00:00.123456Z");

    [Fact]
    public async Task PreservesExactTimestampAndV1V2SignedAndUnknownFrames()
    {
        var storage = new BrowserLogStorage();
        var service = Create(storage);
        var tap = new MavLinkInspectionTap();
        var v2 = new byte[12];
        v2[0] = 0xFD;
        v2[5] = 1;
        v2[6] = 1;
        // An unsupported dialect message must remain available to future decoders.
        v2[7] = 0xFE;
        v2[8] = 0xCA;
        var signed = new byte[25];
        v2.CopyTo(signed, 0);
        signed[2] = 1;
        for (var i = 12; i < signed.Length; i++)
        {
            signed[i] = (byte)i;
        }

        await using (service.Start(tap))
        {
            Publish(tap, V1);
            Publish(tap, v2);
            Publish(tap, signed);
            Publish(tap, V1, MavLinkTrafficDirection.Outbound);
        }

        Assert.Equal("Completed", service.Current.State);
        Assert.False(tap.HasObservers);
        var item = Assert.Single(await storage.ListAsync(LogStorageArea.Telemetry, TestContext.Current.CancellationToken));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}\.tlog$", item.Name);
        await using var stream = await storage.OpenReadAsync(LogStorageArea.Telemetry, item.Id, TestContext.Current.CancellationToken);
        using var expected = new MemoryStream();
        var timestamp = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(timestamp, (ulong)((Received - DateTimeOffset.UnixEpoch).Ticks / 10));
        foreach (var packet in new[] { V1, v2, signed })
        {
            expected.Write(timestamp);
            expected.Write(packet);
        }

        using var actual = new MemoryStream();
        await stream.CopyToAsync(actual, TestContext.Current.CancellationToken);
        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(actual.Length, service.Current.BytesWritten);
        stream.Position = 0;
        var reader = new TelemetryLogReader();
        var index = await reader.IndexAsync(stream, item.Name, TestContext.Current.CancellationToken);
        Assert.Equal(3, index.Entries.Count);
        var last = await reader.ReadAsync(stream, index.Entries[2], TestContext.Current.CancellationToken);
        Assert.Equal(signed, last.Packet.ToArray());
    }

    [Fact]
    public async Task EmptyConnectionsDoNotCreateAbandonedFiles()
    {
        var storage = new BrowserLogStorage();
        var service = Create(storage);
        await using (service.Start(new MavLinkInspectionTap()))
        {
            Assert.Equal("Recording", service.Current.State);
        }

        Assert.Empty(await storage.ListAsync(LogStorageArea.Telemetry, TestContext.Current.CancellationToken));
        Assert.Equal("Completed", service.Current.State);
    }

    [Fact]
    public async Task CollisionUsesNumericSuffixWithoutOverwriting()
    {
        var storage = new BrowserLogStorage();
        // Cover the next few seconds so the test does not depend on a clock boundary.
        for (var second = -1; second < 10; second++)
        {
            var name = DateTimeOffset.UtcNow.AddSeconds(second).ToString("yyyy-MM-dd HH-mm-ss") + ".tlog";
            await using var existing = await storage.CreateAsync(LogStorageArea.Telemetry, name, TestContext.Current.CancellationToken);
        }

        var service = Create(storage);
        var tap = new MavLinkInspectionTap();
        await using (service.Start(tap))
        {
            Publish(tap, V1);
        }

        Assert.EndsWith("-1.tlog", service.Current.FilePath);
    }

    [Fact]
    public async Task StorageFailureIsIsolatedAndObserverReleased()
    {
        var service = Create(new BrowserLogStorage(capacity: 1));
        var tap = new MavLinkInspectionTap();
        await using (service.Start(tap))
        {
            Publish(tap, V1);
        }

        Assert.Equal("Error", service.Current.State);
        Assert.NotNull(service.Current.Error);
        Assert.False(tap.HasObservers);
    }

    [Fact]
    public async Task CompletingObserverPreservesQueue()
    {
        var tap = new MavLinkInspectionTap();
        using var lease = tap.Subscribe();
        Publish(tap, V1);
        lease.Complete();
        Assert.False(tap.HasObservers);
        Assert.True(lease.Reader.TryRead(out _));
        Assert.False(await lease.Reader.WaitToReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectionOwnsRecordingLifetimeAndDoesNotRecordSends()
    {
        var storage = new BrowserLogStorage();
        var service = Create(storage);
        var client = Substitute.For<MissionPlanner.MavLink.Client.IMavLinkClient>();
        var input = System.Threading.Channels.Channel.CreateUnbounded<MissionPlanner.MavLink.Client.PooledMavLinkDataReceived>();
        client.ReceivedBytes.Returns(input.Reader);
        await using var connection = new MavLinkConnection(client,
            Substitute.For<MissionPlanner.MavLink.Services.Abstractions.IMavLinkFrameParser>(),
            Substitute.For<MissionPlanner.MavLink.Services.Abstractions.IMavLinkMessageDecodeHandler>(),
            Substitute.For<IEventHub>(),
            Microsoft.Extensions.Options.Options.Create(new MissionPlanner.MavLink.Client.MavLinkConnectionPipelineOptions()),
            NullLogger<MavLinkConnection>.Instance, trafficRecording: service);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Recording", service.Current.State);
        await connection.SendRawAsync(V1, new TransportEndPoint("test", "recording"), TestContext.Current.CancellationToken);
        await connection.StopAsync();
        Assert.Equal("Completed", service.Current.State);
        Assert.Empty(await storage.ListAsync(LogStorageArea.Telemetry, TestContext.Current.CancellationToken));
    }

    private static void Publish(MavLinkInspectionTap tap, byte[] packet, MavLinkTrafficDirection direction = MavLinkTrafficDirection.Inbound)
    {
        var frame = new MavLinkFrame(1, 1, new TransportEndPoint("test", "recording"), 0, 0,
            ReadOnlyMemory<byte>.Empty, packet, Received);
        tap.Publish(new(direction, frame, null, true));
    }

    private static TelemetryRecordingService Create(ILogStorage storage)
        => new(storage, Substitute.For<IDomainEventHub>(), NullLogger<TelemetryRecordingService>.Instance);
}
