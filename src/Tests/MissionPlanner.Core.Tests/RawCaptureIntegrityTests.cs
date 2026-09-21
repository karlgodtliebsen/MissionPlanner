using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Proves wire-byte integrity through parser, decoder, diagnostic journal and JSON export.</summary>
public sealed class RawCaptureIntegrityTests
{
    /// <summary>Messages required by the capture integrity task, in both supported wire versions.</summary>
    public static IEnumerable<object[]> Messages()
    {
        foreach (uint id in new uint[] { 0, 30, 27, 65, 36, 33, 253, 77 })
        {
            yield return new object[] { id, false, false };
            yield return new object[] { id, true, false };
            yield return new object[] { id, true, true };
        }
    }

    /// <summary>Overwriting reads and reusing/resetting the parser cannot change retained diagnostic bytes.</summary>
    [Theory]
    [MemberData(nameof(Messages))]
    public async Task CompleteFrameSurvivesReadBufferReuseAndExport(uint messageId, bool v2, bool signed)
    {
        var definitions = new MavLinkMessageDefinitionRegistry();
        Assert.True(definitions.TryGet(messageId, out var definition));
        var payload = Enumerable.Range(1, v2 ? definition!.MaximumPayloadLength : definition!.MinimumPayloadLength)
            .Select(value => (byte)value).ToArray();
        var wire = Packet(definition, payload, v2, signed);
        var expectedWire = wire.ToArray();
        var parser = new MavLinkV2FrameParser(definitions);
        var frame = Assert.Single(parser.Parse(wire, new("test"), DateTimeOffset.UtcNow));
        var decoder = new MavLinkMessageDecoders(new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions);
        Assert.True(decoder.TryDecode(frame, out var decoded));
        Assert.NotNull(decoded);

        using var telemetry = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(telemetry, domain, TimeProvider.System, Options.Create(new VehicleLiveDiagnosticOptions()));
        var tap = new MavLinkInspectionTap();
        var connection = Substitute.For<IMavLinkConnection>();
        connection.Inspection.Returns(tap);
        var session = Substitute.For<IVehicleConnectionSession>();
        session.Connection.Returns(connection);
        using var source = new VehicleRawDiagnosticsSource(domain, telemetry, session, definitions, NullLogger<VehicleRawDiagnosticsSource>.Instance);
        var vehicle = new VehicleId(1, 1);
        await domain.PublishDomainEventAsync(new VehicleConnected(vehicle, "test", "test", frame.ReceivedAt), TestContext.Current.CancellationToken);
        tap.Publish(new(MavLinkTrafficDirection.Inbound, frame, decoded, true));
        Array.Fill(wire, (byte)0xAA);
        parser.Reset();
        parser.Parse(expectedWire, new("test"), DateTimeOffset.UtcNow);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetRaw(vehicle).Count == 1);

        var raw = Assert.Single(diagnostics.GetRaw(vehicle));
        Assert.Equal(decoded.MessageId, raw.MessageId);
        Assert.Equal(decoded.MessageId, raw.DecodedMessageId);
        Assert.Equal(payload.Length, raw.PayloadLength);
        Assert.Equal(frame.Payload.ToArray(), raw.PayloadBytes.ToArray());
        Assert.Equal(payload, raw.PayloadBytes.ToArray());
        Assert.Equal(expectedWire, raw.FrameBytes.ToArray());
        Assert.Equal(expectedWire.Length, raw.FrameLength);
        Assert.True(raw.CrcVerified);
        Assert.Equal(signed, raw.Signed);
        Assert.Equal(v2 ? 2 : 1, raw.WireVersion);

        using var json = JsonDocument.Parse(diagnostics.CreateSnapshotJson(vehicle));
        var exported = json.RootElement.GetProperty("Raw")[0];
        Assert.Equal(Convert.ToHexString(payload), exported.GetProperty("Payload").GetString());
        Assert.Equal(payload.Length, exported.GetProperty("PayloadLength").GetInt32());
        Assert.Equal(payload, exported.GetProperty("PayloadBytes").EnumerateArray().Select(b => b.GetByte()).ToArray());
        Assert.Equal(expectedWire, exported.GetProperty("FrameBytes").EnumerateArray().Select(b => b.GetByte()).ToArray());
    }

    /// <summary>Three-byte timestamp-only v2 payloads are valid wire messages, not capture fragments.</summary>
    [Theory]
    [InlineData(30u)]
    [InlineData(65u)]
    [InlineData(36u)]
    public void ZeroTrimmedPayloadIsPreservedAndExplained(uint id)
    {
        var definitions = new MavLinkMessageDefinitionRegistry();
        Assert.True(definitions.TryGet(id, out var definition));
        var wire = Packet(definition!, new byte[] { 0x92, 0xE1, 0x6E }, true, false);
        var frame = Assert.Single(new MavLinkV2FrameParser(definitions).Parse(wire, new("test"), DateTimeOffset.UtcNow));
        var decoder = new MavLinkMessageDecoders(new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions);
        Assert.True(decoder.TryDecode(frame, out var decoded));
        var raw = VehicleRawDiagnostic.Capture(new(1, 1), new(MavLinkTrafficDirection.Inbound, frame, decoded, true), definition);
        Assert.Equal(3, raw.PayloadLength);
        Assert.Equal("92E16E", raw.Payload);
        Assert.Equal(15, raw.FrameLength);
        Assert.Contains("zero-trimmed", raw.Summary);
        Assert.Equal(definition!.MaximumPayloadLength, raw.DecodedPayloadLength);
    }

    /// <summary>The diagnostic model owns bytes even if a non-parser producer uses mutable memory.</summary>
    [Fact]
    public void DiagnosticBoundaryCopiesMutableProducerBytes()
    {
        byte[] payload = [1, 2, 3];
        byte[] frameBytes = [0xFD, 3, 0, 0, 0, 1, 1, 30, 0, 0, 1, 2, 3, 0, 0];
        var frame = new MavLinkFrame(1, 1, new("test"), 30, 0, payload, frameBytes, DateTimeOffset.UtcNow);
        var raw = VehicleRawDiagnostic.Capture(new(1, 1), new(MavLinkTrafficDirection.Inbound, frame, null, false), null);
        Array.Fill(payload, (byte)9);
        Array.Fill(frameBytes, (byte)9);
        Assert.Equal("010203", raw.Payload);
        Assert.Equal(0xFD, raw.FrameBytes[0]);
        Assert.False(raw.CrcVerified);
    }

    private static byte[] Packet(MavLinkMessageDefinition definition, byte[] payload, bool v2, bool signed)
    {
        var header = v2 ? 10 : 6;
        var packet = new byte[header + payload.Length + 2 + (signed ? 13 : 0)];
        packet[0] = v2 ? (byte)0xFD : (byte)0xFE;
        packet[1] = (byte)payload.Length;
        if (v2)
        {
            packet[2] = signed ? (byte)1 : (byte)0;
            packet[5] = 1;
            packet[6] = 1;
            packet[7] = (byte)definition.MessageId;
            packet[8] = (byte)(definition.MessageId >> 8);
            packet[9] = (byte)(definition.MessageId >> 16);
        }
        else
        {
            packet[3] = 1;
            packet[4] = 1;
            packet[5] = (byte)definition.MessageId;
        }
        payload.CopyTo(packet, header);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(header + payload.Length),
            MavLinkCrc.Calculate(packet.AsSpan(1, header - 1 + payload.Length), definition.CrcExtra));
        return packet;
    }
}
