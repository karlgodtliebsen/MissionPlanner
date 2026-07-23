using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies deterministic telemetry-log playback, isolation, safety, and cleanup.</summary>
[Trait("TestTier", "Unit")]
public sealed class SimulationPlaybackDiagnosticsTests
{
    /// <summary>Verifies indexing, random packet reads, duration, and non-monotonic timestamp normalization.</summary>
    [Fact]
    public async Task TelemetryLogReaderIndexesAndReadsTimestampedFrames()
    {
        await using var provider = CreateReplayProvider(new CapturingReplayDelay());
        var crc = provider.GetRequiredService<IMavLinkCrcExtraProvider>();
        var first = MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 1, customMode: 10);
        var second = MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 2, customMode: 20);
        await using var stream = CreateTelemetryLog(
            (DateTimeOffset.Parse("2026-07-23T10:00:00Z"), first),
            (DateTimeOffset.Parse("2026-07-23T10:00:02Z"), second),
            (DateTimeOffset.Parse("2026-07-23T10:00:01Z"), first));
        var reader = new TelemetryLogReader();

        var index = await reader.IndexAsync(stream, "flight.tlog", TestContext.Current.CancellationToken);
        var record = await reader.ReadAsync(stream, index.Entries[1], TestContext.Current.CancellationToken);

        index.Entries.Should().HaveCount(3);
        index.Duration.Should().Be(TimeSpan.FromSeconds(2));
        index.AdjustedTimestampCount.Should().Be(1);
        index.Entries.Select(entry => entry.FrameNumber).Should().Equal(0, 1, 2);
        record.Packet.ToArray().Should().Equal(second);
    }

    /// <summary>Verifies seek reconstruction and speed-adjusted replay-clock intervals through the real decoder pipeline.</summary>
    [Fact]
    public async Task ReplaySeeksReconstructsStateAndUsesSpeedAdjustedClock()
    {
        var capturedDelay = new CapturingReplayDelay();
        await using var provider = CreateReplayProvider(capturedDelay);
        var crc = provider.GetRequiredService<IMavLinkCrcExtraProvider>();
        var manager = provider.GetRequiredService<IReplaySessionManager>();
        var start = DateTimeOffset.Parse("2026-07-23T10:00:00Z");
        var stream = CreateTelemetryLog(
            (start, MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 1, customMode: 10)),
            (start.AddSeconds(1), MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 2, customMode: 20)),
            (start.AddSeconds(3), MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 3, customMode: 30)));

        await manager.LoadAsync(stream, "clock.tlog", TestContext.Current.CancellationToken);
        manager.SetSpeed(2);
        await manager.PlayAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => manager.Snapshot.State == ReplaySessionState.Completed);

        manager.Snapshot.Vehicles.Should().ContainSingle();
        manager.Snapshot.Vehicles[0].CustomMode.Should().Be(30);
        manager.Snapshot.Clock!.Elapsed.Should().Be(TimeSpan.FromSeconds(3));
        capturedDelay.Delays.Should().Equal(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1));

        var seeked = await manager.SeekAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        seeked.NextFrameIndex.Should().Be(2);
        seeked.Vehicles.Should().ContainSingle();
        seeked.Vehicles[0].CustomMode.Should().Be(20);
        seeked.Clock!.Elapsed.Should().Be(TimeSpan.FromSeconds(2));
        await manager.CloseAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies pause cancels an active replay delay without consuming the next indexed frame.</summary>
    [Fact]
    public async Task ReplayPauseStopsAtIndexedFrameBoundary()
    {
        var blockingDelay = new BlockingReplayDelay();
        var pipeline = new CountingReplayPipeline();
        await using var manager = new ReplaySessionManager(
            new TelemetryLogReader(),
            pipeline,
            blockingDelay,
            Substitute.For<ILogger<ReplaySessionManager>>());
        var crc = new CommonMavLinkCrcExtraProvider();
        var startedAt = DateTimeOffset.Parse("2026-07-23T10:00:00Z");
        await manager.LoadAsync(
            CreateTelemetryLog(
                (startedAt, MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 1)),
                (startedAt.AddMinutes(1), MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: 2))),
            "pause.tlog",
            TestContext.Current.CancellationToken);

        await manager.PlayAsync(TestContext.Current.CancellationToken);
        await blockingDelay.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var paused = await manager.PauseAsync(TestContext.Current.CancellationToken);

        paused.State.Should().Be(ReplaySessionState.Paused);
        paused.NextFrameIndex.Should().Be(1);
        paused.Clock!.IsRunning.Should().BeFalse();
        pipeline.ProcessCount.Should().Be(1);
        blockingDelay.WasCancelled.Should().BeTrue();
    }

    /// <summary>Verifies the MAVLink connection itself cannot transmit while replay is loaded.</summary>
    [Fact]
    public async Task ReplayPolicyProhibitsSendAtConnectionAndClientBoundaries()
    {
        var delay = new CapturingReplayDelay();
        await using var provider = CreateReplayProvider(delay);
        var manager = provider.GetRequiredService<IReplaySessionManager>();
        var crc = provider.GetRequiredService<IMavLinkCrcExtraProvider>();
        await manager.LoadAsync(
            CreateTelemetryLog((DateTimeOffset.UtcNow, MavLinkKnownFrames.CreateHeartbeatV2(crc))),
            "read-only.tlog",
            TestContext.Current.CancellationToken);
        var client = Substitute.For<IMavLinkClient>();
        var connection = new MavLinkConnection(
            client,
            Substitute.For<IMavLinkFrameParser>(),
            Substitute.For<IMavLinkMessageDecodeHandler>(),
            Substitute.For<IEventHub>(),
            Options.Create(new MavLinkConnectionPipelineOptions()),
            Substitute.For<ILogger<MavLinkConnection>>(),
            new ReplayTransmissionPolicy(manager));

        var send = async () => await connection.SendRawAsync(
            new byte[] { 1, 2, 3 },
            new TransportEndPoint("UDP", "127.0.0.1", 14550),
            TestContext.Current.CancellationToken);

        await send.Should().ThrowAsync<MavLinkTransmissionProhibitedException>();
        await client.DidNotReceive().SendAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<TransportEndPoint>(),
            Arg.Any<CancellationToken>());

        var transport = Substitute.For<IMavLinkTransport>();
        transport.IsConnected.Returns(true);
        await using var guardedClient = new MavLinkClient(
            transport,
            Options.Create(new MavLinkClientPipelineOptions()),
            new DateTimeProvider(DateTimeOffset.UtcNow),
            Substitute.For<ILogger<MavLinkClient>>(),
            new ReplayTransmissionPolicy(manager));
        var directSend = async () => await guardedClient.SendAsync(
            new byte[] { 4, 5, 6 },
            new TransportEndPoint("UDP", "127.0.0.1", 14550),
            TestContext.Current.CancellationToken);
        await directSend.Should().ThrowAsync<MavLinkTransmissionProhibitedException>();
        await transport.DidNotReceive().WriteAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<TransportEndPoint>(),
            Arg.Any<CancellationToken>());

        await manager.CloseAsync(TestContext.Current.CancellationToken);
        await connection.SendRawAsync(
            new byte[] { 1 },
            new TransportEndPoint("UDP", "127.0.0.1", 14550),
            TestContext.Current.CancellationToken);
        await client.Received(1).SendAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<TransportEndPoint>(),
            Arg.Any<CancellationToken>());
        await guardedClient.SendAsync(
            new byte[] { 1 },
            new TransportEndPoint("UDP", "127.0.0.1", 14550),
            TestContext.Current.CancellationToken);
        await transport.Received(1).WriteAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<TransportEndPoint>(),
            Arg.Any<CancellationToken>());
        await connection.DisposeAsync();
    }

    /// <summary>Verifies repeated runs release owned streams and restore the transmission policy.</summary>
    [Fact]
    public async Task RepeatedReplayRunsDoNotLeakOwnedStreamsOrSafetyState()
    {
        var pipeline = new CountingReplayPipeline();
        await using var manager = new ReplaySessionManager(
            new TelemetryLogReader(),
            pipeline,
            new CapturingReplayDelay(),
            Substitute.For<ILogger<ReplaySessionManager>>());
        var crc = new CommonMavLinkCrcExtraProvider();

        for (var run = 0; run < 10; run++)
        {
            var stream = new TrackingMemoryStream(CreateTelemetryLogBytes(
                (DateTimeOffset.UtcNow, MavLinkKnownFrames.CreateHeartbeatV2(crc, sequence: (byte)run))));
            await manager.LoadAsync(stream, $"run-{run}.tlog", TestContext.Current.CancellationToken);
            await manager.PlayAsync(TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => manager.Snapshot.State == ReplaySessionState.Completed);
            await manager.CloseAsync(TestContext.Current.CancellationToken);

            stream.WasDisposed.Should().BeTrue();
            manager.Snapshot.Should().BeSameAs(ReplaySessionSnapshot.Unloaded);
        }

        pipeline.ProcessCount.Should().Be(10);
        pipeline.ResetCount.Should().BeGreaterThanOrEqualTo(20);
    }

    /// <summary>Verifies diagnostic bundles identify active replay and include its bounded clock/frame statistics.</summary>
    [Fact]
    public void DiagnosticBundleIncludesReadOnlyReplayStatistics()
    {
        var startedAt = DateTimeOffset.Parse("2026-07-23T10:00:00Z");
        var index = new TelemetryLogIndex(
            "diagnostic.tlog",
            100,
            [new TelemetryLogIndexEntry(0, 0, 8, 21, startedAt)],
            startedAt,
            startedAt,
            0);
        var replay = Substitute.For<IReplaySessionManager>();
        replay.Snapshot.Returns(new ReplaySessionSnapshot(
            Guid.NewGuid(),
            ReplaySessionState.Paused,
            index,
            1,
            new ReplayClockSnapshot(startedAt, TimeSpan.Zero, TimeSpan.Zero, 4, false),
            [],
            1,
            0,
            "Paused",
            null));

        var document = new SimulationDiagnosticsService(replay).CreateBundle(SimulationSessionSnapshot.Stopped);

        document.Should().Contain("diagnostic.tlog");
        document.Should().Contain("\"transmission\": \"prohibited\"");
        document.Should().Contain("\"decodedFrames\": 1");
        document.Should().Contain("\"speed\": 4");
    }

    private static ServiceProvider CreateReplayProvider(IReplayDelay replayDelay)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider>(new DateTimeProvider(DateTimeOffset.Parse("2026-07-23T10:00:00Z")));
        services.AddDomainServices(configuration);
        services.AddMavLinkServices(configuration);
        services.Replace(ServiceDescriptor.Singleton(replayDelay));
        services.Replace(ServiceDescriptor.Singleton<IReplayDelay>(replayDelay));
        return services.BuildServiceProvider();
    }

    private static MemoryStream CreateTelemetryLog(params (DateTimeOffset Timestamp, byte[] Packet)[] records) =>
        new(CreateTelemetryLogBytes(records), writable: false);

    private static byte[] CreateTelemetryLogBytes(params (DateTimeOffset Timestamp, byte[] Packet)[] records)
    {
        using var stream = new MemoryStream();
        Span<byte> timestamp = stackalloc byte[8];
        foreach (var record in records)
        {
            var microseconds = checked((ulong)((record.Timestamp.ToUniversalTime() - DateTimeOffset.UnixEpoch).Ticks / 10));
            BinaryPrimitives.WriteUInt64BigEndian(timestamp, microseconds);
            stream.Write(timestamp);
            stream.Write(record.Packet);
        }

        return stream.ToArray();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class CapturingReplayDelay : IReplayDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingReplayDelay : IReplayDelay
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WasCancelled { get; private set; }

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                WasCancelled = true;
                throw;
            }
        }
    }

    private sealed class CountingReplayPipeline : IReplayTelemetryPipeline
    {
        public IReadOnlyList<MissionPlanner.Core.Vehicles.Models.VehicleState> Vehicles => [];

        public int ProcessCount { get; private set; }

        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;

        public ValueTask<bool> ProcessAsync(
            ReadOnlyMemory<byte> packet,
            DateTimeOffset receivedAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessCount++;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer, writable: false)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
