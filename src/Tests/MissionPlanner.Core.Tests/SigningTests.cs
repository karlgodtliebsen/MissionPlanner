using System.Text.Json;
using System.Buffers;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.Advanced.Inspector;
using MissionPlanner.Core.Setup.Advanced.Signing;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.MavLink.Signing;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class SigningTests
{
    private const ulong timestamp = 1234567890123;
    private static readonly byte[] key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] packet = Convert.FromHexString("fd0900000001010000000000000002030003034f5b");
    private static readonly CommonMavLinkCrcExtraProvider crc = new(new MavLinkMessageDefinitionRegistry());

    [Fact]
    public void MatchesPymavlinkFixtureAndEveryMutatedByteFailsAuthentication()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mavlink-signing.json")));
        var root = fixture.RootElement;
        var signed = MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp);
        Assert.Equal(root.GetProperty("signed").GetString(), Convert.ToHexString(signed).ToLowerInvariant());
        Assert.True(MavLinkSigningCodec.Verify(signed, key));
        Assert.Equal(timestamp, MavLinkSigningCodec.ReadTimestamp(signed));
        for (var index = 0; index < signed.Length; index++)
        {
            var mutated = signed.ToArray();
            mutated[index] ^= 1;
            Assert.False(MavLinkSigningCodec.Verify(mutated, key));
        }
    }

    [Fact]
    public void ReplayStateIsPerSourceAndNeverUpdatedByInvalidSignatures()
    {
        using var context = new MavLinkSigningContext(key, 7, timestamp);
        var signed = MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp);
        Assert.Equal(MavLinkSignatureStatus.Verified, context.Verify(signed));
        Assert.Equal(MavLinkSignatureStatus.Replay, context.Verify(signed));
        Assert.Equal(MavLinkSignatureStatus.Replay, context.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 8, timestamp - 6_000_001)));
        Assert.Equal(MavLinkSignatureStatus.Verified, context.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 8, timestamp)));
        Assert.Equal(MavLinkSignatureStatus.Invalid, context.Verify(MavLinkSigningCodec.Sign(packet, new byte[32], 50, 7, timestamp + 1000)));
        Assert.Equal(timestamp, context.Timestamp);
        Assert.Equal(MavLinkSignatureStatus.Verified, context.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp + 1)));
    }

    [Fact]
    public void TimestampFloorMonotonicityAndBoundaryNeverWrap()
    {
        Assert.Equal(0UL, MavLinkSigningCodec.Timestamp(MavLinkSigningCodec.Epoch));
        Assert.Equal(1UL, MavLinkSigningCodec.Timestamp(MavLinkSigningCodec.Epoch.AddTicks(100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MavLinkSigningCodec.Timestamp(MavLinkSigningCodec.Epoch.AddTicks(-1)));
        using var context = new MavLinkSigningContext(key, 7, timestamp + 100);
        Assert.Equal(timestamp + 101, MavLinkSigningCodec.ReadTimestamp(context.Sign(packet, 50, timestamp)));
        Assert.Equal(timestamp + 102, MavLinkSigningCodec.ReadTimestamp(context.Sign(packet, 50, timestamp)));
        using var boundary = new MavLinkSigningContext(key, 7, MavLinkSigningCodec.MaximumTimestamp - 1);
        Assert.Equal(MavLinkSigningCodec.MaximumTimestamp, MavLinkSigningCodec.ReadTimestamp(boundary.Sign(packet, 50, 0)));
        Assert.Throws<InvalidOperationException>(() => boundary.Sign(packet, 50, 0));
    }

    [Fact]
    public async Task SetupIsStagedAndTimestampIsReservedBeforeSigning()
    {
        using var session = new MavLinkSigningSession(crc, new Clock());
        var confirmed = session.BeginSetup(key, 7, 1, 1, timestamp);
        Assert.Equal(packet, (await session.SignAsync(packet, TestContext.Current.CancellationToken)).ToArray());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CommitSetupAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken));
        Assert.False(confirmed.IsCompleted);
        session.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp));
        await confirmed;
        ulong reserved = 0;
        await session.CommitSetupAsync((upper, _) => { reserved = upper; return Task.CompletedTask; }, TestContext.Current.CancellationToken);
        var signed = await session.SignAsync(packet, TestContext.Current.CancellationToken);
        Assert.True(MavLinkSigningCodec.Verify(signed.Span, key));
        Assert.InRange(MavLinkSigningCodec.ReadTimestamp(signed.Span), timestamp + 1, reserved);
        Assert.Equal("Active", session.Snapshot().State);
        var prior = session.Snapshot().Fingerprint;
        _ = session.BeginSetup(new byte[32], 8, 1, 1, timestamp + 1);
        session.CancelSetup();
        Assert.Equal("Active", session.Snapshot().State);
        Assert.Equal(prior, session.Snapshot().Fingerprint);
        session.Dispose();
        Assert.Equal("Disabled", session.Snapshot().State);
    }

    [Fact]
    public async Task SecretStoreRestoresFloorAndFailureDoesNotEnableSigning()
    {
        var store = new Store();
        var repository = new SigningKeyRepository(store);
        var prepared = await repository.PrepareAsync(key, 7, timestamp, TestContext.Current.CancellationToken);
        await repository.ReserveAsync(prepared.Id, timestamp + 100, TestContext.Current.CancellationToken);
        using var restored = await new SigningKeyRepository(store).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(timestamp + 100, restored!.Timestamp);
        Assert.Equal(key, restored.Key);
        Assert.DoesNotContain(Convert.ToHexString(key), restored.ToString());
        using var session = new MavLinkSigningSession(crc, new Clock());
        _ = session.BeginSetup(key, 7, 1, 1, timestamp);
        session.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp));
        await Assert.ThrowsAsync<IOException>(() => session.CommitSetupAsync((_, _) => throw new IOException("Storage unavailable"), TestContext.Current.CancellationToken));
        Assert.Equal("Configuring", session.Snapshot().State);
        Assert.Equal(packet, (await session.SignAsync(packet, TestContext.Current.CancellationToken)).ToArray());
        session.CancelSetup();
        Assert.Equal("Disabled", session.Snapshot().State);
    }

    [Fact]
    public void KeyValidationAndInspectionNeverExposeSetupPayloads()
    {
        var invalidKey = new string('Z', 64);
        Assert.DoesNotContain(invalidKey, Assert.Throws<ArgumentException>(() => MavLinkSigningCodec.Import(invalidKey)).ToString());
        Assert.Equal(key, MavLinkSigningCodec.Import(Convert.ToHexString(key)));
        Assert.Equal(16, MavLinkSigningCodec.Fingerprint(key).Length);
        var tap = new MavLinkInspectionTap();
        using var observer = tap.Subscribe();
        var endpoint = new TransportEndPoint("test");
        var frame = new MavLinkFrame(1, 1, endpoint, 256, 0, key, key, DateTimeOffset.UtcNow);
        var observation = new MavLinkInspectionObservation(MavLinkTrafficDirection.Outbound, frame,
            new SetupSigningMessage(1, 1, endpoint, 1, 1, key, timestamp, DateTimeOffset.UtcNow), true);
        tap.Publish(observation);
        Assert.False(observer.Reader.TryRead(out _));
        var aggregate = new InspectorAggregator(new MavLinkMessageDefinitionRegistry(), new Clock());
        aggregate.Observe(observation);
        Assert.Empty(aggregate.Rows(null));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("timeout")]
    [InlineData("disconnect")]
    [InlineData("cancel")]
    [InlineData("wrong-key")]
    public async Task WorkflowWaitsForEvidenceAndReleasesOperation(string outcome)
    {
        var clock = new Clock();
        var active = Substitute.For<IActiveVehicleContext>();
        var id = new VehicleId(1, 1);
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        using var disconnect = new CancellationTokenSource();
        using var cancel = new CancellationTokenSource();
        active.ConnectionCancellationToken.Returns(disconnect.Token);
        var registry = Substitute.For<IVehicleRegistry>();
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
            clock.GetUtcNow(), VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        registry.GetRequired(id).Returns(new VehicleSession(state, new TransportEndPoint("test"), Substitute.For<IDateTimeProvider>()));
        using var signing = new MavLinkSigningSession(crc, clock);
        var transport = Substitute.For<IMavLinkConnection>();
        transport.Signing.Returns(signing);
        var connection = Substitute.For<IVehicleConnectionSession>();
        connection.Connection.Returns(transport);
        var operations = new VehicleOperationGate();
        var service = new SigningSetupService(connection, active, registry, operations, new MavLinkWireMessageEncoder(crc),
            new SigningKeyRepository(new Store()), clock, new());
        transport.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Assert.Equal("Configuring", signing.Snapshot().State);
                if (outcome == "success") { signing.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp + 1)); }
                else if (outcome == "disconnect") { disconnect.Cancel(); }
                else if (outcome == "cancel") { cancel.Cancel(); }
                else
                {
                    if (outcome == "wrong-key")
                    {
                        Assert.Equal(MavLinkSignatureStatus.Invalid, signing.Verify(MavLinkSigningCodec.Sign(packet, new byte[32], 50, 7, timestamp + 1)));
                    }
                    clock.Expire();
                }
                return ValueTask.CompletedTask;
            });
        if (outcome == "success")
        {
            await service.ConfigureAsync(key, 7, true, cancel.Token);
            Assert.Equal("Active", signing.Snapshot().State);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ConfigureAsync(key, 7, true, cancel.Token));
            Assert.Equal("Disabled", signing.Snapshot().State);
        }
        Assert.Null(operations.GetCurrentOperation(id));
        await transport.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task PipelineLabelsRejectedSignaturesAndOnlyPublishesVerifiedOrUnsignedTraffic()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var clock = new Clock();
        using var signing = new MavLinkSigningSession(crc, clock);
        _ = signing.BeginSetup(key, 7, 1, 1, timestamp);
        signing.Verify(MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp));
        await signing.CommitSetupAsync((_, _) => Task.CompletedTask, deadline.Token);
        var queue = Channel.CreateUnbounded<PooledMavLinkDataReceived>();
        var client = Substitute.For<IMavLinkClient>();
        client.ReceivedBytes.Returns(queue.Reader);
        var endpoint = new TransportEndPoint("test");
        var message = new HeartbeatMessage(1, 1, endpoint, 0, 2, 3, 0, 3, 3, clock.GetUtcNow());
        var decoder = Substitute.For<IMavLinkMessageDecodeHandler>();
        decoder.TryDecode(Arg.Any<MavLinkFrame>(), out Arg.Any<MavLinkMessage?>()).Returns(call => { call[1] = message; return true; });
        var hub = Substitute.For<IEventHub>();
        var count = 0;
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.PublishAsync<MavLinkMessage>(Arg.Any<string>(), Arg.Any<MavLinkMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => { if (Interlocked.Increment(ref count) == 2) { delivered.TrySetResult(); } return Task.CompletedTask; });
        var tap = new MavLinkInspectionTap();
        using var lease = tap.Subscribe();
        await using var connection = new MavLinkConnection(client, new MavLinkV2FrameParser(new MavLinkMessageDefinitionRegistry()),
            decoder, hub, Options.Create(new MavLinkConnectionPipelineOptions()), NullLogger<MavLinkConnection>.Instance, inspection: tap, signing: signing);
        await connection.StartAsync(deadline.Token);
        var valid = MavLinkSigningCodec.Sign(packet, key, 50, 7, timestamp + 1);
        var invalid = MavLinkSigningCodec.Sign(packet, new byte[32], 50, 7, timestamp + 2);
        foreach (var bytes in new[] { valid, valid, invalid, packet })
        {
            var memory = MemoryPool<byte>.Shared.Rent(bytes.Length);
            bytes.CopyTo(memory.Memory.Span);
            await queue.Writer.WriteAsync(new(memory, bytes.Length, endpoint, clock.GetUtcNow()), deadline.Token);
        }
        var statuses = new List<MavLinkSignatureStatus>();
        for (var index = 0; index < 4; index++) { statuses.Add((await lease.Reader.ReadAsync(deadline.Token)).Signature); }
        Assert.Equal(new[] { MavLinkSignatureStatus.Verified, MavLinkSignatureStatus.Replay, MavLinkSignatureStatus.Invalid, MavLinkSignatureStatus.Unsigned }, statuses);
        await delivered.Task.WaitAsync(deadline.Token);
        queue.Writer.TryComplete();
        await connection.StopAsync();
        Assert.Equal(2, count);
    }

    private sealed class Store : IPlannerSecretStore
    {
        private readonly Dictionary<string, string> values = [];
        public ValueTask<string?> GetAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(values.GetValueOrDefault(id));
        public ValueTask SetAsync(string id, string value, CancellationToken cancellationToken = default) { values[id] = value; return ValueTask.CompletedTask; }
        public ValueTask RemoveAsync(string id, CancellationToken cancellationToken = default) { values.Remove(id); return ValueTask.CompletedTask; }
    }

    private sealed class Clock : TimeProvider
    {
        private Timer? timer;
        public override DateTimeOffset GetUtcNow() => MavLinkSigningCodec.Epoch.AddTicks((long)timestamp * 100);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => timer = new(callback, state);
        public void Expire() => timer!.Fire();
        private sealed class Timer(TimerCallback callback, object? state) : ITimer
        {
            public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
