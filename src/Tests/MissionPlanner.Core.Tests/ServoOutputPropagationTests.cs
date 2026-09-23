using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises servo wire decoding through authoritative state and live diagnostic export.</summary>
public sealed class ServoOutputPropagationTests
{
    /// <summary>The replay pipeline uses the same catalog and produces identical servo state from recorded wire bytes.</summary>
    [Fact]
    public async Task LiveAndReplayServoStateMatch()
    {
        using var fixture = new Fixture();
        await fixture.ConnectAsync(Fixture.First);
        var frame = await fixture.ReceiveAsync(Fixture.First, QuadPayload()[..12]);
        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoder = new MavLinkMessageDecoderHandler(
            new MavLinkMessageDecoders(new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions),
            NullLogger<MavLinkMessageDecoderHandler>.Instance);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(fixture.Clock.Now);
        var replay = new ReplayTelemetryPipeline(new MavLinkV2FrameParser(definitions), decoder, clock, NullLoggerFactory.Instance);
        byte[] heartbeat = [0, 0, 0, 0, 2, 3, 0, 4, 3];
        Assert.True(await replay.ProcessAsync(Wire(heartbeat, true, Fixture.First, 0, 50), fixture.Clock.Now, TestContext.Current.CancellationToken));
        Assert.True(await replay.ProcessAsync(frame.RawBytes, fixture.Clock.Now, TestContext.Current.CancellationToken));
        var live = fixture.Registry.GetRequired(Fixture.First)!.State.Radio;
        var recorded = Assert.Single(replay.Vehicles).Radio;
        Assert.Equal(live.ServoOutputsRaw, recorded.ServoOutputsRaw);
        Assert.Equal(live.ServoOutputPort, recorded.ServoOutputPort);
        Assert.Equal(live.ServoObservedAt, recorded.ServoObservedAt);
    }

    /// <summary>Full v1 and zero-trimmed v2 frames reach the registered servo handler and raw export.</summary>
    [Theory]
    [InlineData(false, 21)]
    [InlineData(true, 12)]
    [InlineData(true, 11)]
    [InlineData(true, 36)]
    [InlineData(true, 37)]
    public async Task ValidFramesPopulateStateAndExport(bool v2, int length)
    {
        using var fixture = new Fixture();
        await fixture.ConnectAsync(Fixture.First);
        var payload = QuadPayload();
        if (length == 11)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), 200);
        }
        if (length >= 36)
        {
            payload[20] = 2;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(35), length == 36 ? (ushort)200 : (ushort)1800);
        }
        var frame = await fixture.ReceiveAsync(Fixture.First, payload[..length], v2);
        var radio = fixture.Registry.GetRequired(Fixture.First)!.State.Radio;
        Assert.Equal(payload[20], radio.ServoOutputPort);
        Assert.Equal(fixture.Clock.Now, radio.ServoObservedAt);
        Assert.Equal(16, radio.ServoOutputsRaw!.Count);
        for (var index = 0; index < 16; index++)
        {
            var offset = index < 8 ? 4 + index * 2 : 21 + (index - 8) * 2;
            Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset)), radio.ServoOutputsRaw[index]);
        }
        var lines = fixture.Diagnostics.GetOutputs(Fixture.First);
        Assert.Contains(lines, line => line.StartsWith("SERVO_OUTPUT_RAW received."));
        Assert.DoesNotContain("No SERVO_OUTPUT_RAW received.", lines);
        Assert.Contains("Physical motor movement remains unknown.", lines);

        using var export = JsonDocument.Parse(fixture.Diagnostics.CreateSnapshotJson(Fixture.First));
        var root = export.RootElement;
        var exportedRadio = root.GetProperty("Vehicle").GetProperty("State").GetProperty("Radio");
        Assert.Equal(radio.ServoOutputPort, exportedRadio.GetProperty("ServoOutputPort").GetByte());
        Assert.Equal(radio.ServoObservedAt, exportedRadio.GetProperty("ServoObservedAt").GetDateTimeOffset());
        Assert.Equal(radio.ServoOutputsRaw, exportedRadio.GetProperty("ServoOutputsRaw").EnumerateArray().Select(value => value.GetUInt16()).ToArray());
        var raw = root.GetProperty("Raw")[0];
        Assert.True(raw.GetProperty("CrcVerified").GetBoolean());
        Assert.Equal(length, raw.GetProperty("PayloadLength").GetInt32());
        Assert.Equal(Convert.ToHexString(frame.RawBytes.Span), Convert.ToHexString(raw.GetProperty("FrameBytes").EnumerateArray().Select(value => value.GetByte()).ToArray()));
    }

    /// <summary>The minimum v2 payload represents genuine zero outputs, not missing telemetry.</summary>
    [Fact]
    public async Task SingleZeroByteIsObserved()
    {
        using var fixture = new Fixture();
        await fixture.ConnectAsync(Fixture.First);
        await fixture.ReceiveAsync(Fixture.First, [0]);
        Assert.All(fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoOutputsRaw!, value => Assert.Equal((ushort)0, value));
        Assert.DoesNotContain("No SERVO_OUTPUT_RAW received.", fixture.Diagnostics.GetOutputs(Fixture.First));
    }

    /// <summary>Samples replace values and timestamps independently for each system/component identity.</summary>
    [Fact]
    public async Task LaterSamplesAndOtherVehiclesRemainIndependent()
    {
        using var fixture = new Fixture();
        var second = new VehicleId(2, 1);
        var component = new VehicleId(1, 2);
        await fixture.ConnectAsync(Fixture.First);
        await fixture.ConnectAsync(second);
        await fixture.ConnectAsync(component);
        await fixture.ReceiveAsync(Fixture.First, QuadPayload()[..12]);
        var firstTime = fixture.Clock.Now;
        fixture.Clock.Now = firstTime.AddSeconds(1);
        var payload = QuadPayload();
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), 1500);
        await fixture.ReceiveAsync(second, payload[..12]);
        Assert.Equal((ushort)1000, fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoOutputsRaw![0]);
        Assert.Equal(firstTime, fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoObservedAt);
        Assert.Null(fixture.Registry.GetRequired(component)!.State.Radio.ServoOutputsRaw);
        await fixture.ReceiveAsync(Fixture.First, payload[..12]);
        Assert.Equal((ushort)1500, fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoOutputsRaw![0]);
        Assert.Equal(fixture.Clock.Now, fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoObservedAt);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        await fixture.ReceiveAsync(Fixture.First, payload[..12]);
        Assert.Equal(fixture.Clock.Now, fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoObservedAt);
    }

    /// <summary>Freshness expires without another packet, and reconnect cannot revive an old sample.</summary>
    [Fact]
    public async Task NeverSeenFreshStaleAndReconnectAreDistinct()
    {
        using var fixture = new Fixture();
        await fixture.ConnectAsync(Fixture.First);
        Assert.Contains("No SERVO_OUTPUT_RAW received.", fixture.Diagnostics.GetOutputs(Fixture.First));
        await fixture.ReceiveAsync(Fixture.First, QuadPayload()[..12]);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
        Assert.Contains(fixture.Diagnostics.GetOutputs(Fixture.First), line => line.StartsWith("SERVO_OUTPUT_RAW received."));
        fixture.Clock.Now = fixture.Clock.Now.AddMilliseconds(1);
        Assert.Contains(fixture.Diagnostics.GetOutputs(Fixture.First), line => line.Contains("latest sample is stale"));
        Assert.DoesNotContain("No SERVO_OUTPUT_RAW received.", fixture.Diagnostics.GetOutputs(Fixture.First));
        await fixture.ReceiveAsync(Fixture.First, QuadPayload()[..12]);
        await fixture.Registry.Reset(TestContext.Current.CancellationToken);
        await fixture.Domain.PublishDomainEventAsync(new VehicleDisconnected(Fixture.First, fixture.Clock.Now), TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => fixture.Diagnostics.GetSnapshot(Fixture.First).Disconnected);
        Assert.Contains(fixture.Diagnostics.GetOutputs(Fixture.First), line => line.Contains("latest sample is stale"));
        await fixture.Domain.PublishDomainEventAsync(new VehicleConnected(Fixture.First, "UDP", "test", fixture.Clock.Now), TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => !fixture.Diagnostics.GetSnapshot(Fixture.First).Disconnected);
        Assert.Null(fixture.Diagnostics.GetSnapshot(Fixture.First).State);
        await fixture.RegisterAsync(Fixture.First);
        Assert.Null(fixture.Registry.GetRequired(Fixture.First)!.State.Radio.ServoObservedAt);
        await fixture.ReceiveAsync(Fixture.First, QuadPayload()[..12]);
        Assert.Contains(fixture.Diagnostics.GetOutputs(Fixture.First), line => line.StartsWith("SERVO_OUTPUT_RAW received."));
    }

    /// <summary>Invalid frame lengths and CRCs never enter the state pipeline.</summary>
    [Fact]
    public void ParserRejectsMalformedFrames()
    {
        var parser = new MavLinkV2FrameParser(new MavLinkMessageDefinitionRegistry());
        var endpoint = new TransportEndPoint("test");
        Assert.Empty(parser.Parse(Wire([0], false, Fixture.First), endpoint, DateTimeOffset.UtcNow));
        Assert.Empty(parser.Parse(Wire([], true, Fixture.First), endpoint, DateTimeOffset.UtcNow));
        Assert.Empty(parser.Parse(Wire(new byte[38], true, Fixture.First), endpoint, DateTimeOffset.UtcNow));
        var corrupt = Wire(QuadPayload()[..12], true, Fixture.First);
        corrupt[^1] ^= 1;
        Assert.Empty(parser.Parse(corrupt, endpoint, DateTimeOffset.UtcNow));
    }

    private static byte[] QuadPayload()
    {
        var payload = new byte[37];
        for (var index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4 + index * 2), 1000);
        }
        return payload;
    }

    private static byte[] Wire(byte[] payload, bool v2, VehicleId id, byte messageId = 36, byte crcExtra = 222)
    {
        var header = v2 ? 10 : 6;
        var wire = new byte[header + payload.Length + 2];
        wire[0] = v2 ? (byte)0xfd : (byte)0xfe;
        wire[1] = (byte)payload.Length;
        wire[v2 ? 5 : 3] = id.SystemId;
        wire[v2 ? 6 : 4] = id.ComponentId;
        wire[v2 ? 7 : 5] = messageId;
        payload.CopyTo(wire, header);
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(header + payload.Length), MavLinkCrc.Calculate(wire.AsSpan(1, header - 1 + payload.Length), crcExtra));
        return wire;
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IDisposable
    {
        internal static readonly VehicleId First = new(1, 1);
        internal readonly Clock Clock = new();
        internal readonly DomainEventHub Domain = new(NullLogger<EventHub>.Instance);
        internal readonly VehicleRegistry Registry;
        internal readonly VehicleLiveDiagnostics Diagnostics;
        private readonly EventHub telemetry = new(NullLogger<EventHub>.Instance);
        private readonly MavLinkInspectionTap tap = new();
        private readonly VehicleRawDiagnosticsSource rawSource;
        private readonly MavLinkMessageDecoders decoders;
        private readonly MavLinkV2FrameParser parser;
        private readonly VehicleMessageDispatcher dispatcher;
        private readonly TransportEndPoint endpoint = new("test");

        internal Fixture()
        {
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(_ => Clock.Now);
            Registry = new VehicleRegistry(Domain, clock, NullLogger<VehicleRegistry>.Instance);
            Diagnostics = new VehicleLiveDiagnostics(telemetry, Domain, Clock, Options.Create(new VehicleLiveDiagnosticOptions()));
            var definitions = new MavLinkMessageDefinitionRegistry();
            decoders = new MavLinkMessageDecoders(new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions);
            parser = new MavLinkV2FrameParser(definitions);
            dispatcher = new VehicleMessageDispatcher([new RadioTelemetryHandler(Registry, Domain)]);
            var connection = Substitute.For<IMavLinkConnection>();
            connection.Inspection.Returns(tap);
            var session = Substitute.For<IVehicleConnectionSession>();
            session.Connection.Returns(connection);
            rawSource = new VehicleRawDiagnosticsSource(Domain, telemetry, session, definitions, NullLogger<VehicleRawDiagnosticsSource>.Instance);
        }

        internal async Task ConnectAsync(VehicleId id)
        {
            await Domain.PublishDomainEventAsync(new VehicleConnected(id, "UDP", "test", Clock.Now), TestContext.Current.CancellationToken);
            await RegisterAsync(id);
        }

        internal async Task RegisterAsync(VehicleId id)
        {
            await Registry.RegisterOrUpdateHeartbeatAsync(id, endpoint, 0, 2, 3, 0, 4, 3, Clock.Now, TestContext.Current.CancellationToken);
        }

        internal async Task<MavLinkFrame> ReceiveAsync(VehicleId id, byte[] payload, bool v2 = true)
        {
            var frame = Assert.Single(parser.Parse(Wire(payload, v2, id), endpoint, Clock.Now));
            Assert.True(decoders.TryDecode(frame, out var decoded));
            Assert.IsType<ServoOutputRawMessage>(decoded);
            tap.Publish(new(MavLinkTrafficDirection.Inbound, frame, decoded, true));
            Assert.True(await dispatcher.DispatchAsync(decoded!, TestContext.Current.CancellationToken));
            await VehicleLiveDiagnosticsTests.UntilAsync(() => Diagnostics.GetSnapshot(id).State?.Radio.ServoObservedAt == Clock.Now &&
                Diagnostics.GetRaw(id).FirstOrDefault()?.At == Clock.Now);
            return frame;
        }

        public void Dispose()
        {
            rawSource.Dispose();
            Diagnostics.Dispose();
            telemetry.Dispose();
            Domain.Dispose();
        }
    }
}
