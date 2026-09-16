using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Tests.Fixtures;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Configuration;

namespace MissionPlanner.Core.Tests;

/// <summary>Reproduces bench arming failures through tlog reader, normal decoder and shared telemetry handlers.</summary>
public sealed class BenchTelemetryReplayTests
{
    /// <summary>Seven reduced bench scenarios yield stable domain/UI snapshots and text order across repeated logs.</summary>
    [Theory]
    [InlineData("PreArm: Accels inconsistent", 0, VehicleArmingState.DisarmedNotReady)]
    [InlineData("PreArm: Compass 1 not healthy", 0, VehicleArmingState.DisarmedNotReady)]
    [InlineData("PreArm: Logging failed ENOSPC", 0, VehicleArmingState.DisarmedNotReady)]
    [InlineData("Arm: Roll (RC1) is not neutral", 0, VehicleArmingState.DisarmedNotReady)]
    [InlineData("Arm: Yaw (RC4) is not neutral", 0, VehicleArmingState.DisarmedNotReady)]
    [InlineData("PreArm: Accels inconsistent", 1, VehicleArmingState.DisarmedReady)]
    [InlineData("PreArm: Compass 1 not healthy", 2, VehicleArmingState.Armed)]
    public async Task ReplaysBenchScenarioDeterministically(string reason, int transition, VehicleArmingState expected)
    {
        await using var provider = CreateProvider();
        var pipeline = (ReplayTelemetryPipeline)provider.GetRequiredService<IReplayTelemetryPipeline>();
        await using var manager = new ReplaySessionManager(new TelemetryLogReader(), pipeline,
            new ImmediateReplayDelay(), NullLogger<ReplaySessionManager>.Instance);
        VehicleArmingStatus? previous = null;
        for (var run = 0; run < 2; run++)
        {
            var packets = new List<byte[]>
            {
                BenchReplayFixtures.Heartbeat(), BenchReplayFixtures.Health(false),
                BenchReplayFixtures.Text(reason), BenchReplayFixtures.Text("Unrelated bench message"),
                BenchReplayFixtures.Heartbeat()
            };
            if (transition == 1)
            {
                packets.Add(BenchReplayFixtures.Health(true));
            }
            if (transition == 2)
            {
                packets.Add(BenchReplayFixtures.Heartbeat(true));
            }
            await manager.LoadAsync(BenchReplayFixtures.Log(packets.ToArray()), "synthetic-bench.tlog",
                TestContext.Current.CancellationToken);
            Assert.Empty(pipeline.Vehicles);
            Assert.Empty(pipeline.StatusMessages);
            var completed = await PlayToEndAsync(manager);
            var state = Assert.Single(completed.Vehicles);
            Assert.Equal(expected, state.Arming.State);
            Assert.Equal(0, completed.RejectedFrames);
            Assert.Equal(new[] { reason, "Unrelated bench message" }, pipeline.StatusMessages.Select(text => text.Text));
            if (transition == 0 && reason.StartsWith("PreArm:", StringComparison.Ordinal))
            {
                Assert.Equal(reason, state.Arming.PreArmReason);
            }
            if (reason.StartsWith("Arm:", StringComparison.Ordinal))
            {
                Assert.Equal(reason, state.Arming.LastArmFailure);
            }
            if (reason.Contains("ENOSPC", StringComparison.Ordinal))
            {
                Assert.Contains("ENOSPC", state.OnboardLogging.StorageDetail);
                Assert.True(state.OnboardLogging.AffectsArming);
            }
            if (transition > 0)
            {
                Assert.Null(state.Arming.PreArmReason);
            }
            if (previous is not null)
            {
                Assert.Equal(previous, state.Arming);
            }
            previous = state.Arming;
        }
        await manager.LoadAsync(BenchReplayFixtures.Log(BenchReplayFixtures.Heartbeat()), "new-session.tlog",
            TestContext.Current.CancellationToken);
        var reset = await PlayToEndAsync(manager);
        Assert.Equal(VehicleArmingStatus.Empty with { UpdatedAt = BenchReplayFixtures.Start }, Assert.Single(reset.Vehicles).Arming);
        Assert.Empty(pipeline.StatusMessages);
        await manager.CloseAsync(TestContext.Current.CancellationToken);
        Assert.Empty(pipeline.Vehicles);
    }

    /// <summary>Chunk assembly and expiry use recorded time, and incomplete assemblies cannot leak between logs.</summary>
    [Fact]
    public async Task ChunkAssemblyUsesRecordedTimeAndResets()
    {
        await using var provider = CreateProvider();
        var pipeline = (ReplayTelemetryPipeline)provider.GetRequiredService<IReplayTelemetryPipeline>();
        await using var manager = new ReplaySessionManager(new TelemetryLogReader(), pipeline,
            new ImmediateReplayDelay(), NullLogger<ReplaySessionManager>.Instance);
        var first = "PreArm: " + new string('a', 42);
        await manager.LoadAsync(BenchReplayFixtures.TimedLog(
            (0, BenchReplayFixtures.Heartbeat()),
            (1, BenchReplayFixtures.Text(first, 12)),
            (2, BenchReplayFixtures.Text(" complete", 12, 1)),
            (3, BenchReplayFixtures.Text(first, 13)),
            (7, BenchReplayFixtures.Text("after timeout"))), "chunks.tlog", TestContext.Current.CancellationToken);
        await PlayToEndAsync(manager);
        Assert.Equal(new[] { first + " complete", first, "after timeout" },
            pipeline.StatusMessages.Select(text => text.Text));
        Assert.True(pipeline.StatusMessages[1].IsTruncated);

        await manager.LoadAsync(BenchReplayFixtures.Log(BenchReplayFixtures.Heartbeat(),
            BenchReplayFixtures.Text(first, 20)), "incomplete.tlog", TestContext.Current.CancellationToken);
        await PlayToEndAsync(manager);
        Assert.Empty(pipeline.StatusMessages);
        await manager.LoadAsync(BenchReplayFixtures.Log(BenchReplayFixtures.Heartbeat(),
            BenchReplayFixtures.Text("new log", 20, 1)), "replacement.tlog", TestContext.Current.CancellationToken);
        await PlayToEndAsync(manager);
        Assert.Equal("new log", Assert.Single(pipeline.StatusMessages).Text);
    }

    private static async Task<ReplaySessionSnapshot> PlayToEndAsync(IReplaySessionManager manager)
    {
        var finished = new TaskCompletionSource<ReplaySessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(ReplaySessionChangedEventArgs change)
        {
            if (change.Snapshot.State is ReplaySessionState.Completed or ReplaySessionState.Failed)
            {
                finished.TrySetResult(change.Snapshot);
            }
        }
        manager.Changed += Changed;
        try
        {
            await manager.PlayAsync(TestContext.Current.CancellationToken);
            var result = await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(ReplaySessionState.Completed, result.State);
            return result;
        }
        finally
        {
            manager.Changed -= Changed;
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider>(new DateTimeProvider(BenchReplayFixtures.Start));
        services.AddDomainServices(configuration);
        services.AddMavLinkServices(configuration);
        return services.BuildServiceProvider();
    }
}
