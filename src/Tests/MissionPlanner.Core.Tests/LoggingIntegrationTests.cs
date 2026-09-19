using System.Buffers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Tests.Fixtures;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Logging;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class LoggingIntegrationTests
{
    [Fact]
    public async Task DesktopStartupWritesConfiguredFileAndHistorySurvivesLoggerRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "MissionPlanner-log-integration-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = DesktopConfiguration();
            var paths = new DesktopLogPathProvider(_ => root);
            using (var provider = new ServiceCollection().AddSingleton<ILogPathProvider>(paths)
                       .AddLogging(config).BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Integration");
                logger.LogWarning("Persistence test {VehicleId}", "vehicle-42");
                var buffer = provider.GetRequiredService<ApplicationLogBuffer>();
                Assert.Contains(buffer.Snapshot(), entry => entry.SourceContext == "Integration" &&
                    entry.Properties.ContainsKey("VehicleId"));
                var state = provider.GetRequiredService<ApplicationLogFileState>();
                Assert.True(state.FileEnabled);
                Assert.StartsWith(paths.ApplicationLogDirectory, state.CurrentFile);
            }

            using var restarted = new ServiceCollection().AddSingleton<ILogPathProvider>(paths)
                .AddLogging(config).BuildServiceProvider();
            var history = restarted.GetRequiredService<ApplicationLogHistory>();
            var file = Assert.Single(await history.ListAsync(TestContext.Current.CancellationToken));
            var events = await history.ReadAsync(file.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(events, entry => entry.RenderedMessage.Contains("vehicle-42"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task BrowserCompositionUsesMemoryEvenWithDesktopFileConfiguration()
    {
        using var provider = new ServiceCollection().AddLogStorage(browser: true)
            .AddLogging(DesktopConfiguration()).BuildServiceProvider();
        Assert.Null(provider.GetService<ILogPathProvider>());
        var storage = Assert.IsType<BrowserLogStorage>(provider.GetRequiredService<ILogStorage>());
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Browser.Integration");
        logger.LogWarning("Browser live event");
        Assert.Contains(provider.GetRequiredService<ApplicationLogBuffer>().Snapshot(), entry => entry.RenderedMessage == "Browser live event");
        Assert.False(provider.GetRequiredService<ApplicationLogFileState>().FileEnabled);
        Assert.Null(provider.GetRequiredService<ApplicationLogFileState>().CurrentFile);
        var recorder = new TelemetryRecordingService(storage, Substitute.For<IDomainEventHub>(), NullLogger<TelemetryRecordingService>.Instance);
        var tap = new MavLinkInspectionTap();
        var packet = BenchReplayFixtures.Heartbeat();
        await using (recorder.Start(tap))
        {
            tap.Publish(new(MavLinkTrafficDirection.Inbound,
                new MavLinkFrame(1, 1, new TransportEndPoint("browser"), 0, 0, packet.AsMemory(10, 9), packet,
                    BenchReplayFixtures.Start), null, true));
        }

        var file = Assert.Single(await storage.ListAsync(LogStorageArea.Telemetry, TestContext.Current.CancellationToken));
        await using var export = await storage.ExportAsync(LogStorageArea.Telemetry, file.Id, TestContext.Current.CancellationToken);
        var imported = await provider.GetRequiredService<LogFileOperations>()
            .ImportAsync(LogStorageArea.Telemetry, file.Name, export.Content, TestContext.Current.CancellationToken);
        Assert.NotEqual(file.Id, imported);
        await using var read = await storage.OpenReadAsync(LogStorageArea.Telemetry, imported, TestContext.Current.CancellationToken);
        var index = await new TelemetryLogReader().IndexAsync(read, imported, TestContext.Current.CancellationToken);
        Assert.Single(index.Entries);
    }

    [Theory]
    [InlineData("serial")]
    [InlineData("udp")]
    [InlineData("tcp")]
    public async Task CommonConnectionRecordsValidatedInputBeforeDecoderAndFlushesOnDisconnect(string transport)
    {
        var storage = new BrowserLogStorage();
        var recorder = new TelemetryRecordingService(storage, Substitute.For<IDomainEventHub>(), NullLogger<TelemetryRecordingService>.Instance);
        var client = Substitute.For<IMavLinkClient>();
        var channel = System.Threading.Channels.Channel.CreateUnbounded<PooledMavLinkDataReceived>();
        client.ReceivedBytes.Returns(channel.Reader);
        client.Completion.Returns((Task?)null);
        var healthClock = new MissionPlanner.Test.Support.ManualTimeProvider(DateTimeOffset.UtcNow);
        using var decoder = new BlockingDecoder();
        await using var connection = new MavLinkConnection(client,
            new MavLinkV2FrameParser(new MavLinkMessageDefinitionRegistry()), decoder,
            Substitute.For<IEventHub>(), Options.Create(new MavLinkConnectionPipelineOptions()),
            NullLogger<MavLinkConnection>.Instance, trafficRecording: recorder, clock: healthClock);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
            var packet = BenchReplayFixtures.Heartbeat();
            var memory = MemoryPool<byte>.Shared.Rent(packet.Length);
            packet.CopyTo(memory.Memory.Span);
            await channel.Writer.WriteAsync(new(memory, packet.Length, new TransportEndPoint(transport, "test"),
                BenchReplayFixtures.Start), TestContext.Current.CancellationToken);
            await decoder.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            while (recorder.Current.BytesWritten == 0)
            {
                await Task.Delay(10, timeout.Token);
            }

            // Decoder is still blocked: persistence cannot depend on decoded domain messages.
            Assert.False(decoder.Release.IsSet);
            Assert.NotNull(connection.Activity.LastPacketAt(1));
            decoder.Release.Set();
            var registry = Substitute.For<MissionPlanner.Core.Vehicles.Abstractions.IVehicleRegistry>();
            var vehicleId = new MissionPlanner.Shared.Models.Vehicles.Models.VehicleId(1, 1);
            var state = new MissionPlanner.Core.Vehicles.Models.VehicleState(vehicleId, 0, 2, 3, 0, 4, 3,
                MissionPlanner.Shared.Models.Vehicles.Models.VehicleConnectionState.Online, healthClock.GetUtcNow(),
                MissionPlanner.Shared.Models.Vehicles.Models.VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            registry.GetRequired(vehicleId).Returns(new MissionPlanner.Core.Vehicles.VehicleSession(state, new TransportEndPoint(transport),
                Substitute.For<MissionPlanner.Library.DateTime.Domain.IDateTimeProvider>()));
            var applicationSession = Substitute.For<MissionPlanner.Core.Vehicles.Abstractions.IVehicleConnectionSession>();
            applicationSession.Connection.Returns(connection);
            await using var monitor = new MissionPlanner.Core.Vehicles.VehicleConnectionMonitor(registry,
                Substitute.For<IDomainEventHub>(), healthClock, Options.Create(new MissionPlanner.Core.Vehicles.VehicleConnectionHealthOptions()),
                NullLogger<MissionPlanner.Core.Vehicles.VehicleConnectionMonitor>.Instance);
            var stopCount = 0;
            using var lease = monitor.Track(vehicleId, Guid.NewGuid(), applicationSession, async _ =>
            {
                Interlocked.Increment(ref stopCount);
                await connection.StopAsync();
            });
            healthClock.Advance(TimeSpan.FromSeconds(11));
            await monitor.UpdateConnectionStatesAsync(TestContext.Current.CancellationToken);
            await monitor.UpdateConnectionStatesAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, stopCount);
            Assert.Equal("Completed", recorder.Current.State);
            var file = Assert.Single(await storage.ListAsync(LogStorageArea.Telemetry, TestContext.Current.CancellationToken));
            await using var stream = await storage.OpenReadAsync(LogStorageArea.Telemetry, file.Id, TestContext.Current.CancellationToken);
            var reader = new TelemetryLogReader();
            var index = await reader.IndexAsync(stream, file.Name, TestContext.Current.CancellationToken);
            var entry = Assert.Single(index.Entries);
            Assert.Equal(packet, (await reader.ReadAsync(stream, entry, TestContext.Current.CancellationToken)).Packet.ToArray());
        }
        finally
        {
            decoder.Release.Set();
        }
    }

    private static IConfiguration DesktopConfiguration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "UI", "MissionPlanner.App", "appsettings.json");
            if (File.Exists(path))
            {
                return new ConfigurationBuilder().AddJsonFile(path).Build();
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find repository application configuration.");
    }

    private sealed class BlockingDecoder : IMavLinkMessageDecodeHandler, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();

        public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
        {
            Entered.TrySetResult();
            Release.Wait(TimeSpan.FromSeconds(10));
            message = null;
            return false;
        }

        public void Dispose() => Release.Dispose();
    }
}
