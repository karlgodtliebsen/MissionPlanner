using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Protects typed common telemetry and live/replay parity against MAVLink 2 trailing-zero trimming.</summary>
public sealed class CommonTelemetryTrimTests
{
    /// <summary>Known binary values decode and update the same state in live dispatch and isolated replay.</summary>
    [Theory]
    [InlineData(178, false)]
    [InlineData(178, true)]
    [InlineData(74, false)]
    [InlineData(74, true)]
    [InlineData(24, false)]
    [InlineData(24, true)]
    [InlineData(125, false)]
    [InlineData(125, true)]
    public async Task KnownValuesReachLiveAndReplay(int messageId, bool trim)
    {
        var definitions = new MavLinkMessageDefinitionRegistry();
        Assert.True(definitions.TryGet((uint)messageId, out var definition));
        var decoder = new MavLinkMessageDecoderHandler(new MavLinkMessageDecoders(
            new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions), NullLogger<MavLinkMessageDecoderHandler>.Instance);
        var at = DateTimeOffset.UtcNow;
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(at);
        var endpoint = new TransportEndPoint("test");
        var state = VehicleLiveDiagnosticsTests.State(new(1, 1));
        var session = new VehicleSession(state, endpoint, clock);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(state.VehicleId).Returns(session);
        var events = Substitute.For<IDomainEventHub>();
        var dispatcher = new VehicleMessageDispatcher([new FlightTelemetryHandler(registry, events),
            new NavigationTelemetryHandler(registry, events), new PowerTelemetryHandler(registry, events)]);
        var payload = Payload(messageId, definition!.MaximumPayloadLength);
        if (trim)
        {
            var length = payload.Length;
            while (length > 1 && payload[length - 1] == 0)
            {
                length--;
            }
            payload = payload[..length];
        }
        var packet = Packet((byte)messageId, definition.CrcExtra, payload);
        var parser = new MavLinkV2FrameParser(definitions);
        var frame = Assert.Single(parser.Parse(packet, endpoint, at));
        Assert.True(decoder.TryDecode(frame, out var message));
        switch (messageId)
        {
            case 178:
                var ahrs = Assert.IsType<Ahrs2Message>(message);
                Assert.Equal(1.25f, ahrs.Roll);
                Assert.Equal(50, ahrs.Altitude);
                Assert.Equal(1, ahrs.Latitude);
                Assert.Equal(0, ahrs.Longitude);
                break;
            case 74:
                var hud = Assert.IsType<VfrHudMessage>(message);
                Assert.Equal(12.5f, hud.Airspeed);
                Assert.Equal(20, hud.Groundspeed);
                Assert.Equal(123, hud.Heading);
                Assert.Equal(0, hud.Throttle);
                break;
            case 24:
                var gps = Assert.IsType<GpsRawIntMessage>(message);
                Assert.Equal(55, gps.Latitude);
                Assert.Equal(12, gps.Longitude);
                Assert.Equal(3, gps.FixType);
                Assert.Equal((uint)1500, gps.HorizontalAccuracy);
                break;
            case 125:
                var power = Assert.IsType<PowerStatusMessage>(message);
                Assert.Equal(5000, power.Vcc);
                Assert.Equal(6000, power.Vservo);
                Assert.Equal(1, power.Flags);
                break;
        }
        Assert.True(await dispatcher.DispatchAsync(message!, TestContext.Current.CancellationToken));
        var replay = new ReplayTelemetryPipeline(new MavLinkV2FrameParser(definitions), decoder, clock, NullLoggerFactory.Instance);
        await replay.ProcessAsync(Packet(0, 50, [0, 0, 0, 0, 2, 3, 0, 4, 3]), at, TestContext.Current.CancellationToken);
        await replay.ProcessAsync(packet, at, TestContext.Current.CancellationToken);
        var recorded = Assert.Single(replay.Vehicles);
        Assert.Equal(session.State.Motion, recorded.Motion);
        Assert.Equal(session.State.Position, recorded.Position);
        Assert.Equal(session.State.Estimator, recorded.Estimator);
        Assert.Equal(session.State.Gps, recorded.Gps);
        Assert.Equal(session.State.Power, recorded.Power);
    }

    /// <summary>Primary attitude and global position retain precedence over secondary estimates and HUD.</summary>
    [Fact]
    public void FallbacksCannotOverwriteFreshPrimarySources()
    {
        var at = DateTimeOffset.UtcNow;
        var session = new VehicleSession(VehicleLiveDiagnosticsTests.State(new(1, 1)), new("test"), Substitute.For<IDateTimeProvider>());
        session.ApplyAttitude(new(1, 2, 3, null, null, null, at));
        session.ApplyGlobalPosition(new(55, 12, 100, 20, 3, 4, -2, 90, at));
        session.ApplyAhrsFallback(new(4, 5, 6, 1, 2, 200, true, at.AddMilliseconds(100)));
        session.ApplyHud(new(10, 99, 180, 200, 50, at.AddMilliseconds(100)));
        Assert.Equal(1, session.State.Motion.RollRadians);
        Assert.Equal(100, session.State.Position.AltitudeMslMeters);
        Assert.Equal(5, session.State.Motion.GroundSpeedMetersPerSecond);
        Assert.Equal(10, session.State.Motion.AirSpeedMetersPerSecond);
        Assert.Equal(4, session.State.Estimator.RollRadians);
        session.ApplyHud(new(10, 99, 180, 200, 50, at.AddSeconds(2)));
        session.ApplyAhrsFallback(new(4, 5, 6, 1, 2, 200, true, at.AddSeconds(2)));
        Assert.Equal(200, session.State.Position.AltitudeMslMeters);
        Assert.Equal(4, session.State.Motion.RollRadians);
    }

    private static byte[] Payload(int id, int length)
    {
        var payload = new byte[length];
        switch (id)
        {
            case 178:
                BinaryPrimitives.WriteSingleLittleEndian(payload, 1.25f);
                BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(12), 50);
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16), 10000000);
                break;
            case 74:
                BinaryPrimitives.WriteSingleLittleEndian(payload, 12.5f);
                BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(4), 20);
                BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16), 123);
                break;
            case 24:
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), 550000000);
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12), 120000000);
                payload[28] = 3;
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(34), 1500);
                break;
            case 125:
                BinaryPrimitives.WriteUInt16LittleEndian(payload, 5000);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), 6000);
                payload[4] = 1;
                break;
        }
        return payload;
    }

    private static byte[] Packet(byte id, byte extra, byte[] payload)
    {
        var packet = new byte[payload.Length + 12];
        packet[0] = 0xfd;
        packet[1] = (byte)payload.Length;
        packet[5] = 1;
        packet[6] = 1;
        packet[7] = id;
        payload.CopyTo(packet, 10);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(packet.Length - 2), MavLinkCrc.Calculate(packet.AsSpan(1, packet.Length - 3), extra));
        return packet;
    }
}
