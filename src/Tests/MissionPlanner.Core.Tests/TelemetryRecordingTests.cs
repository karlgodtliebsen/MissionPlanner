using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Replay;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks PC logging format, session ownership, and failure isolation.</summary>
public sealed class TelemetryRecordingTests
{
    /// <summary>Recording drains both directions and reconnect creates a separate readable file.</summary>
    [Fact]
    public async Task RecordsBothDirectionsAndFinalizesOnDisconnect()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MissionPlannerRecording-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = Create(directory);
            var tap = new MavLinkInspectionTap();
            var recording = service.Start(tap);
            Assert.Equal("Recording", service.Current.State);
            var firstPath = service.Current.FilePath!;
            Publish(tap, MavLinkTrafficDirection.Inbound);
            Publish(tap, MavLinkTrafficDirection.Outbound);
            await recording.DisposeAsync();
            Assert.Equal("Completed", service.Current.State);
            Assert.False(tap.HasObservers);
            await using (var file = File.OpenRead(firstPath))
            {
                var reader = new TelemetryLogReader();
                var index = await reader.IndexAsync(file, "recorded", TestContext.Current.CancellationToken);
                Assert.Equal(2, index.Entries.Count);
                var record = await reader.ReadAsync(file, index.Entries[0], TestContext.Current.CancellationToken);
                Assert.Equal(Packet, record.Packet.ToArray());
            }
            await using (service.Start(tap))
            {
                Assert.NotEqual(firstPath, service.Current.FilePath);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    /// <summary>An invalid destination is visible without throwing into the connection pipeline.</summary>
    [Fact]
    public async Task FileCreationFailureIsIsolated()
    {
        var file = Path.GetTempFileName();
        try
        {
            var service = Create(file);
            await using var recording = service.Start(new MavLinkInspectionTap());
            Assert.Equal("Error", service.Current.State);
            Assert.NotNull(service.Current.Error);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Graceful observer completion preserves every queued frame for the writer.</summary>
    [Fact]
    public async Task CompletingObserverPreservesQueue()
    {
        var tap = new MavLinkInspectionTap();
        using var lease = tap.Subscribe();
        Publish(tap, MavLinkTrafficDirection.Inbound);
        lease.Complete();
        Assert.False(tap.HasObservers);
        Assert.True(lease.Reader.TryRead(out _));
        Assert.False(await lease.Reader.WaitToReadAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The production connection starts recording before reception and finalizes it on stop.</summary>
    [Fact]
    public async Task ConnectionOwnsRecordingLifetime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MissionPlannerConnectionRecording-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = Create(directory);
            var client = Substitute.For<MissionPlanner.MavLink.Client.IMavLinkClient>();
            var input = System.Threading.Channels.Channel.CreateUnbounded<MissionPlanner.MavLink.Client.PooledMavLinkDataReceived>();
            client.ReceivedBytes.Returns(input.Reader);
            await using var connection = new MavLinkConnection(
                client,
                Substitute.For<MissionPlanner.MavLink.Services.Abstractions.IMavLinkFrameParser>(),
                Substitute.For<MissionPlanner.MavLink.Services.Abstractions.IMavLinkMessageDecodeHandler>(),
                Substitute.For<IEventHub>(),
                Microsoft.Extensions.Options.Options.Create(new MissionPlanner.MavLink.Client.MavLinkConnectionPipelineOptions()),
                NullLogger<MavLinkConnection>.Instance,
                trafficRecording: service);
            await connection.StartAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Recording", service.Current.State);
            await connection.SendRawAsync(Packet, new TransportEndPoint("test", "recording"), TestContext.Current.CancellationToken);
            await connection.StopAsync();
            Assert.Equal("Completed", service.Current.State);
            Assert.Equal(16, new FileInfo(service.Current.FilePath!).Length);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
    private static readonly byte[] Packet = [0xFE, 0, 0, 1, 1, 0, 0, 0];

    private static void Publish(MavLinkInspectionTap tap, MavLinkTrafficDirection direction)
    {
        var frame = new MavLinkFrame(1, 1, new TransportEndPoint("test", "recording"), 0, 0,
            ReadOnlyMemory<byte>.Empty, Packet, DateTimeOffset.Parse("2026-09-16T12:00:00Z"));
        tap.Publish(new(direction, frame, null, true));
    }

    private static TelemetryRecordingService Create(string directory)
    {
        var settings = Substitute.For<IPlannerSettingsService>();
        settings.Current.Returns(new PlannerSettings { Logging = new PlannerLoggingSettings { LogDirectory = directory } });
        return new(settings, Substitute.For<IDomainEventHub>(), NullLogger<TelemetryRecordingService>.Instance);
    }
}
