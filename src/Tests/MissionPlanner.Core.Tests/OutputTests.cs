using System.Threading.Channels;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.MavLink.Signing;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class OutputTests
{
    private static readonly OutputEndpoint endpoint = new(OutputEndpointKind.Udp, "127.0.0.1", 14551);

    [Theory]
    [InlineData("https://host", 14551)]
    [InlineData("user:secret@host", 14551)]
    [InlineData("", 14551)]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", 65536)]
    public void ProfilesRejectInvalidAddressesAndPorts(string address, int port) => Assert.NotNull((endpoint with { Address = address, Port = port }).Validate());

    [Fact]
    public async Task OverflowDropsNewestAndStopAbortsBlockedWriteBeforeReleasingOwner()
    {
        var sink = new Sink { Block = true };
        var factory = new Factory(() => sink);
        var owners = new OutputEndpointOwners();
        var session = new BoundedOutputSession(factory, owners, TimeProvider.System);
        await session.StartAsync(endpoint, new(Capacity: 1), TestContext.Current.CancellationToken);
        Assert.True(session.Offer([1]));
        await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(session.Offer([2, 2]));
        Assert.False(session.Offer([3, 3, 3]));
        Assert.Equal(1, session.Snapshot().DroppedFrames);
        var other = new BoundedOutputSession(factory, owners, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.StartAsync(endpoint, new(), TestContext.Current.CancellationToken));
        Assert.Equal("Active", session.Snapshot().State);
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(sink.Aborted);
        Assert.True(sink.Disposed);
        Assert.True(session.Completion.IsCompletedSuccessfully);
        Assert.Equal("Stopped", session.Snapshot().State);
        Assert.Equal(3, session.Snapshot().DroppedFrames);
        Assert.Equal(6, session.Snapshot().DroppedBytes);
        using var released = owners.Acquire(endpoint.Identity);
    }

    [Fact]
    public async Task WriteTimeoutFaultsAndReleasesNativeHandleWithFakeTime()
    {
        var clock = new Clock();
        var sink = new Sink { Block = true };
        var session = new BoundedOutputSession(new Factory(() => sink), new(), clock);
        await session.StartAsync(endpoint, new(), TestContext.Current.CancellationToken);
        session.Offer([1]);
        await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("Faulted", session.Snapshot().State);
        Assert.Equal(1, session.Snapshot().DroppedFrames);
        Assert.True(sink.Disposed);
        await session.StopAsync();
    }

    [Fact]
    public async Task ReconnectBudgetHasClockDrivenBackoffAndCanRestart()
    {
        var clock = new Clock();
        var factory = new Factory(() => throw new IOException("private endpoint details"));
        var session = new BoundedOutputSession(factory, new(), clock);
        var start = session.StartAsync(endpoint, new(ReconnectAttempts: 1), TestContext.Current.CancellationToken);
        Assert.Equal(1, factory.Attempts);
        Assert.Equal("Reconnecting", session.Snapshot().State);
        Assert.False(start.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<IOException>(() => start);
        Assert.Equal(2, factory.Attempts);
        Assert.Equal("Faulted", session.Snapshot().State);
        Assert.DoesNotContain("private", session.Snapshot().Error!);
        factory.Create = () => new Sink();
        await session.StartAsync(endpoint, new(), TestContext.Current.CancellationToken);
        await session.StopAsync();
        Assert.Equal("Stopped", session.Snapshot().State);
    }

    [Theory]
    [InlineData(MirrorDirection.Inbound)]
    [InlineData(MirrorDirection.Outbound)]
    [InlineData(MirrorDirection.Both)]
    public async Task MirrorPreservesAcceptedBytesOrderingAndStopsFeedback(MirrorDirection direction)
    {
        var sink = new Sink();
        var output = new BoundedOutputSession(new Factory(() => sink), new(), TimeProvider.System);
        var tap = new MavLinkInspectionTap();
        var connection = Substitute.For<IMavLinkConnection>();
        connection.Inspection.Returns(tap);
        var vehicle = Substitute.For<IVehicleConnectionSession>();
        vehicle.Connection.Returns(connection);
        var mirror = new MirrorSession(vehicle, Substitute.For<IActiveVehicleContext>(), output, TimeProvider.System);
        await mirror.StartAsync(endpoint, new(), direction, true, TestContext.Current.CancellationToken);
        var v2 = Convert.FromHexString("fd0900000001010000000000000002030003034f5b");
        var signed = MavLinkSigningCodec.Sign(v2, new byte[32], 50, 7, 100);
        var v1 = Convert.FromHexString("fe09000101000000000002030003034eb9");
        var unknown = signed.ToArray();
        unknown[7] = unknown[8] = unknown[9] = 0xff;
        var selected = direction == MirrorDirection.Outbound ? MavLinkTrafficDirection.Outbound : MavLinkTrafficDirection.Inbound;
        foreach (var bytes in new[] { v1, v2, signed, unknown })
        {
            tap.Publish(Observation(bytes, selected));
            var written = await sink.Writes.Reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(bytes, written);
            // Simulated reflection re-enters the authoritative pipeline, without creating a return writer.
            tap.Publish(Observation(bytes, selected));
        }
        if (direction != MirrorDirection.Both)
        {
            tap.Publish(Observation([42], selected == MavLinkTrafficDirection.Inbound ? MavLinkTrafficDirection.Outbound : MavLinkTrafficDirection.Inbound));
        }
        tap.Publish(Observation([99], selected));
        Assert.Equal(new byte[] { 99 }, await sink.Writes.Reader.ReadAsync(TestContext.Current.CancellationToken));
        await mirror.StopAsync();
        Assert.Equal(5, mirror.Snapshot().Frames);
        Assert.Equal(4, mirror.Snapshot().DroppedFrames);
        Assert.False(tap.HasObservers);
        Assert.True(sink.Disposed);
        await connection.DidNotReceive().SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>());
        await connection.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task TenRestartsAndConnectionClosureDoNotLeaveObserversOrWriters()
    {
        var output = new BoundedOutputSession(new Factory(() => new Sink()), new(), TimeProvider.System);
        var tap = new MavLinkInspectionTap();
        var connection = Substitute.For<IMavLinkConnection>();
        connection.Inspection.Returns(tap);
        var vehicle = Substitute.For<IVehicleConnectionSession>();
        vehicle.Connection.Returns(connection);
        var mirror = new MirrorSession(vehicle, Substitute.For<IActiveVehicleContext>(), output, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => mirror.StartAsync(endpoint, new(), MirrorDirection.Both, false, TestContext.Current.CancellationToken));
        for (var index = 0; index < 10; index++)
        {
            await mirror.StartAsync(endpoint, new(), MirrorDirection.Inbound, false, TestContext.Current.CancellationToken);
            Assert.True(tap.HasObservers);
            tap.CloseObservers();
            await output.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await mirror.StopAsync();
            Assert.False(tap.HasObservers);
            Assert.Equal("Stopped", mirror.Snapshot().State);
        }
    }

    private static MavLinkInspectionObservation Observation(byte[] bytes, MavLinkTrafficDirection direction) => new(direction,
        new MavLinkFrame(1, 1, new TransportEndPoint("test"), 0, 0, ReadOnlyMemory<byte>.Empty, bytes, DateTimeOffset.UtcNow), null, false);

    private sealed class Factory(Func<IOutputSink> create) : IOutputSinkFactory
    {
        public Func<IOutputSink> Create { get; set; } = create;
        public int Attempts { get; private set; }
        public string? UnavailableReason(OutputEndpoint endpoint) => null;
        public Task<IOutputSink> OpenAsync(OutputEndpoint endpoint, CancellationToken token) { Attempts++; return Task.FromResult(Create()); }
    }

    private sealed class Sink : IOutputSink
    {
        public bool Block { get; init; }
        public bool Aborted { get; private set; }
        public bool Disposed { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<byte[]> Writes { get; } = Channel.CreateUnbounded<byte[]>();
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            Entered.TrySetResult();
            if (Block) { await aborted.Task; token.ThrowIfCancellationRequested(); }
            Writes.Writer.TryWrite(data.ToArray());
        }
        public void Abort() { Aborted = true; aborted.TrySetResult(); }
        public ValueTask DisposeAsync() { Disposed = true; Abort(); return ValueTask.CompletedTask; }
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.Parse("2026-09-07T00:00:00Z");
        private readonly List<Timer> timers = [];
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(callback, state, now + dueTime);
            lock (timers) { timers.Add(timer); }
            return timer;
        }
        public void Advance(TimeSpan delta)
        {
            now += delta;
            Timer[] current;
            lock (timers) { current = timers.ToArray(); }
            foreach (var timer in current) { timer.Fire(now); }
        }
        private sealed class Timer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            private bool disposed;
            public void Fire(DateTimeOffset now) { if (!disposed && now >= due) { disposed = true; callback(state); } }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
