using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates lossless raw fallback for CRC-valid frames without typed decoders.
/// </summary>
public sealed class MavLinkRawFallbackTests
{
    private static readonly TransportEndPoint TestEndPoint = new("udp", "127.0.0.1", 14550);

    /// <summary>Verifies the complete signed MAVLink 2 envelope and exact payload are retained.</summary>
    [Fact]
    public void KnownUntypedFrameBecomesLosslessRawMessage()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(168, out var wind).Should().BeTrue();
        var payload = Enumerable.Range(1, wind!.MinimumPayloadLength).Select(value => (byte)value).ToArray();
        var signature = Enumerable.Range(101, 13).Select(value => (byte)value).ToArray();
        var packet = BuildFrame(wind, payload, 0x01, 0x55, signature);
        var receivedAt = DateTimeOffset.UtcNow;
        var frame = new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, receivedAt)
            .Should().ContainSingle().Which;
        var decoders = new MavLinkMessageDecoders([new HeartbeatMessageDecoder()], registry);

        decoders.TryDecode(frame, out var decoded).Should().BeTrue();
        var raw = decoded.Should().BeOfType<RawMavLinkMessage>().Subject;
        raw.MavLinkVersion.Should().Be(2);
        raw.Sequence.Should().Be(7);
        raw.SystemId.Should().Be(1);
        raw.ComponentId.Should().Be(2);
        raw.RawMessageId.Should().Be(168);
        raw.IncompatibilityFlags.Should().Be(0x01);
        raw.CompatibilityFlags.Should().Be(0x55);
        raw.IsSigned.Should().BeTrue();
        raw.Signature.Should().Equal(signature);
        raw.Payload.Should().Equal(payload);
        raw.RawFrame.Should().Equal(packet);
        raw.MessageName.Should().Be("WIND");
        raw.Definition.Should().Be(wind);
        raw.ReceivedAt.Should().Be(receivedAt);
    }

    /// <summary>Verifies a successful registered typed decoder takes precedence over raw fallback.</summary>
    [Fact]
    public void TypedDecoderWinsWhenRegistered()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(0, out var heartbeat).Should().BeTrue();
        var packet = BuildFrame(heartbeat!, new byte[heartbeat!.MinimumPayloadLength]);
        var frame = new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().ContainSingle().Which;
        var decoders = new MavLinkMessageDecoders([new HeartbeatMessageDecoder()], registry);

        decoders.TryDecode(frame, out var decoded).Should().BeTrue();
        decoded.Should().BeOfType<HeartbeatMessage>();
    }

    /// <summary>Verifies a typed decoder failure degrades to raw rather than discarding a valid frame.</summary>
    [Fact]
    public void TypedDecoderFailureFallsBackToRawMessage()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(168, out var wind).Should().BeTrue();
        var packet = BuildFrame(wind!, new byte[wind!.MinimumPayloadLength]);
        var frame = new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().ContainSingle().Which;
        var decoders = new MavLinkMessageDecoders([new RejectingDecoder(168)], registry);

        decoders.TryDecode(frame, out var decoded).Should().BeTrue();
        decoded.Should().BeOfType<RawMavLinkMessage>();
    }

    /// <summary>Verifies an invalid CRC is rejected before the raw fallback boundary.</summary>
    [Fact]
    public void InvalidCrcNeverBecomesRawMessage()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(168, out var wind).Should().BeTrue();
        var packet = BuildFrame(wind!, new byte[wind!.MinimumPayloadLength]);
        packet[10 + wind.MinimumPayloadLength] ^= 0xff;

        new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
    }

    /// <summary>Verifies unknown future IDs are rejected at CRC validation and remain trace-diagnosable.</summary>
    [Fact]
    public void UnknownFutureIdFollowsDocumentedParserPolicy()
    {
        var unknown = new MavLinkMessageDefinition(60001, "FUTURE_TEST", 17, 1, 1, "future", false);
        var packet = BuildFrame(unknown, [42]);
        var logger = new RecordingLogger<MavLinkV2FrameParser>();

        new MavLinkV2FrameParser(new MavLinkMessageDefinitionRegistry(), logger)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();

        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Trace
            && entry.Message.Contains("unknown message", StringComparison.Ordinal)
            && entry.Message.Contains("60001", StringComparison.Ordinal));
    }

    /// <summary>Verifies raw fallback diagnostics stay below warning level.</summary>
    [Fact]
    public void RawFallbackDoesNotProduceWarningSpam()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(168, out var wind).Should().BeTrue();
        var packet = BuildFrame(wind!, new byte[wind!.MinimumPayloadLength]);
        var frame = new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().ContainSingle().Which;
        var logger = new RecordingLogger<MavLinkMessageDecoderHandler>();
        var handler = new MavLinkMessageDecoderHandler(new MavLinkMessageDecoders([], registry), logger);

        handler.TryDecode(frame, out var message).Should().BeTrue();
        message.Should().BeOfType<RawMavLinkMessage>();
        logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("WIND", StringComparison.Ordinal));
        logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
    }

    /// <summary>Verifies raw fallback no longer relies on a fictitious message-ID constant.</summary>
    [Fact]
    public void NoDefaultFallbackMessageIdExists()
    {
        typeof(MessageIds).GetField("DefaultFallback").Should().BeNull();
    }

    private static byte[] BuildFrame(
        MavLinkMessageDefinition definition,
        ReadOnlySpan<byte> payload,
        byte incompatibilityFlags = 0,
        byte compatibilityFlags = 0,
        ReadOnlySpan<byte> signature = default)
    {
        var signed = (incompatibilityFlags & 0x01) != 0;
        if (signed && signature.Length != 13)
        {
            throw new ArgumentException("A signed MAVLink 2 frame requires exactly 13 signature bytes.", nameof(signature));
        }

        var packet = new byte[10 + payload.Length + 2 + (signed ? 13 : 0)];
        packet[0] = 0xfd;
        packet[1] = checked((byte)payload.Length);
        packet[2] = incompatibilityFlags;
        packet[3] = compatibilityFlags;
        packet[4] = 7;
        packet[5] = 1;
        packet[6] = 2;
        packet[7] = (byte)definition.MessageId;
        packet[8] = (byte)(definition.MessageId >> 8);
        packet[9] = (byte)(definition.MessageId >> 16);
        payload.CopyTo(packet.AsSpan(10));
        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, 9 + payload.Length), definition.CrcExtra);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10 + payload.Length), crc);
        if (signed)
        {
            signature.CopyTo(packet.AsSpan(packet.Length - 13));
        }

        return packet;
    }

    private sealed class RejectingDecoder(uint messageId) : IMavLinkMessageDecoder
    {
        public uint MessageId { get; } = messageId;

        public byte CrcExtra => 0;

        public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
        {
            message = null;
            return false;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
