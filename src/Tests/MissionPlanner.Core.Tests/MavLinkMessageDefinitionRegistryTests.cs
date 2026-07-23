using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Generator;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates the generated MAVLink message-definition registry and parser integration.
/// </summary>
public sealed class MavLinkMessageDefinitionRegistryTests
{
    private const string SourceRevision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";
    private static readonly TransportEndPoint TestEndPoint = new("test");

    /// <summary>Verifies every resolved official definition has one identical runtime entry.</summary>
    [Fact]
    public void RegistryMatchesEveryResolvedDialectDefinition()
    {
        var official = LoadOfficialDefinitions();
        var registry = new MavLinkMessageDefinitionRegistry();

        registry.Definitions.Should().HaveCount(official.Count);
        registry.Definitions.Select(definition => definition.MessageId).Should().OnlyHaveUniqueItems();
        foreach (var expected in official)
        {
            registry.TryGet(expected.MessageId, out var actual).Should().BeTrue();
            actual.Should().Be(new MavLinkMessageDefinition(
                expected.MessageId,
                expected.Name,
                expected.CrcExtra,
                expected.MinimumPayloadLength,
                expected.MaximumPayloadLength,
                expected.Dialect,
                expected.IsDeprecated));
        }
    }

    /// <summary>Verifies the committed generated source exactly matches deterministic generation.</summary>
    [Fact]
    public void GeneratedRegistrySourceIsCurrent()
    {
        var expected = MavLinkRegistrySourceGenerator.Generate(LoadOfficialDefinitions(), SourceRevision);
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "Core",
            "MissionPlanner.MavLink",
            "Generated",
            "MavLinkMessageDefinitions.g.cs");

        File.ReadAllText(path).Should().Be(expected);
    }

    /// <summary>Verifies the compatibility CRC provider obtains every value from the registry.</summary>
    [Fact]
    public void CrcCompatibilityProviderCoversEntireRegistry()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var provider = new CommonMavLinkCrcExtraProvider(registry);

        foreach (var definition in registry.Definitions)
        {
            provider.TryGetCrcExtra(definition.MessageId, out var crcExtra).Should().BeTrue();
            crcExtra.Should().Be(definition.CrcExtra);
        }
    }

    /// <summary>Verifies messages formerly absent from the hand-written CRC switch now parse.</summary>
    /// <param name="messageId">The official message identifier.</param>
    [Theory]
    [InlineData(163u)]
    [InlineData(168u)]
    [InlineData(241u)]
    [InlineData(11030u)]
    public void FormerlyUnknownArduPilotFramesPassCrcValidation(uint messageId)
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(messageId, out var definition).Should().BeTrue();
        var packet = BuildFrame(definition!, new byte[definition!.MinimumPayloadLength]);

        var frames = new MavLinkV2FrameParser(registry).Parse(packet, TestEndPoint, DateTimeOffset.UtcNow);

        frames.Should().ContainSingle().Which.MessageId.Should().Be(messageId);
    }

    /// <summary>Verifies MAVLink 2 may truncate trailing zero bytes below the base-field length.</summary>
    [Fact]
    public void MavLink2PayloadLengthsFromOneToMaximumAreAccepted()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(24, out var gpsRawInt).Should().BeTrue();
        var definition = gpsRawInt!;
        var lengths = new[]
        {
            1,
            definition.MinimumPayloadLength - 1,
            definition.MinimumPayloadLength,
            checked((byte)(definition.MinimumPayloadLength + 1)),
            definition.MaximumPayloadLength
        }.Distinct();

        foreach (var length in lengths)
        {
            var packet = BuildFrame(definition, new byte[length]);
            var frames = new MavLinkV2FrameParser(registry).Parse(packet, TestEndPoint, DateTimeOffset.UtcNow);
            frames.Should().ContainSingle().Which.Payload.Length.Should().Be(length);
        }
    }

    /// <summary>Verifies version-specific payload bounds reject malformed frames.</summary>
    [Fact]
    public void PayloadLengthsOutsideVersionSpecificBoundsAreRejected()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(24, out var gpsRawInt).Should().BeTrue();
        var definition = gpsRawInt!;
        var emptyMavLink2Payload = BuildFrame(definition, []);
        var aboveMaximum = BuildFrame(definition, new byte[definition.MaximumPayloadLength + 1]);
        var shortMavLink1Payload = BuildFrame(definition, new byte[definition.MinimumPayloadLength - 1], mavLink2: false);
        var logger = new RecordingLogger<MavLinkV2FrameParser>();

        new MavLinkV2FrameParser(registry, logger)
            .Parse(emptyMavLink2Payload, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
        new MavLinkV2FrameParser(registry, logger)
            .Parse(aboveMaximum, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
        new MavLinkV2FrameParser(registry, logger)
            .Parse(shortMavLink1Payload, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
        logger.Messages.Should().Contain(message =>
            message.Contains("GPS_RAW_INT", StringComparison.Ordinal)
            && message.Contains("BasePayloadLength=30", StringComparison.Ordinal)
            && message.Contains("MaximumPayloadLength=52", StringComparison.Ordinal));
    }

    /// <summary>Verifies a short ArduPilot MAVFTP ACK survives frame validation and is zero-padded by its decoder.</summary>
    [Fact]
    public async Task TruncatedMavFtpResponseReachesResponseRegistration()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(MessageIds.FileTransferProtocol, out var definition).Should().BeTrue();
        var payload = new byte[9];
        payload[1] = 255;
        payload[2] = 190;
        payload[3] = 1;
        payload[6] = (byte)MavFtpOpcode.Ack;
        payload[8] = (byte)MavFtpOpcode.ListDirectory;
        var packet = BuildFrame(definition!, payload);

        var frame = new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().ContainSingle().Which;
        var decoder = new FileTransferProtocolMessageDecoder();

        decoder.TryDecode(frame, out var decoded).Should().BeTrue();
        var message = decoded.Should().BeOfType<FileTransferProtocolMessage>().Subject;
        var eventHub = new EventHub(NullLogger<EventHub>.Instance);
        using var dispatcher = new MavFtpResponseDispatcher(
            eventHub,
            new MavFtpPacketCodec(),
            Options.Create(new MavFtpOptions()),
            NullLogger<MavFtpResponseDispatcher>.Instance);
        var target = new MavFtpTarget(1, 1, TestEndPoint);
        using var responseRegistration = dispatcher.Register(target, 0, MavFtpOpcode.ListDirectory, 0);

        await eventHub.PublishAsync<MavLinkMessage>(
            MavLinkEventTopics.ReceivedMessage,
            message,
            TestContext.Current.CancellationToken);
        var response = await responseRegistration.ReadAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        response.Sequence.Should().Be(1);
        response.Opcode.Should().Be(MavFtpOpcode.Ack);
        response.RequestedOpcode.Should().Be(MavFtpOpcode.ListDirectory);
    }

    /// <summary>Verifies a defined zero-payload message is accepted.</summary>
    [Fact]
    public void DefinedZeroLengthPayloadIsAccepted()
    {
        var definition = new MavLinkMessageDefinition(60000, "TEST_EMPTY", 17, 0, 0, "test", false);
        var registry = new SingleDefinitionRegistry(definition);
        var packet = BuildFrame(definition, []);

        var frames = new MavLinkV2FrameParser(registry).Parse(packet, TestEndPoint, DateTimeOffset.UtcNow);

        frames.Should().ContainSingle().Which.Payload.Length.Should().Be(0);
    }

    /// <summary>Verifies a MAVLink signature changes frame size without changing payload validation.</summary>
    [Fact]
    public void SignedFrameLengthIsHandledIndependentlyFromPayloadLength()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(0, out var heartbeat).Should().BeTrue();
        var packet = BuildFrame(heartbeat!, new byte[heartbeat!.MinimumPayloadLength], signed: true);

        var frame = new MavLinkV2FrameParser(registry)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().ContainSingle().Which;

        frame.RawBytes.Length.Should().Be(10 + heartbeat.MinimumPayloadLength + 2 + 13);
    }

    /// <summary>Verifies an otherwise valid official frame with a damaged CRC is rejected.</summary>
    [Fact]
    public void InvalidCrcIsRejected()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(163, out var ahrs).Should().BeTrue();
        var packet = BuildFrame(ahrs!, new byte[ahrs!.MinimumPayloadLength]);
        packet[10 + ahrs.MinimumPayloadLength] ^= 0xff;
        var logger = new RecordingLogger<MavLinkV2FrameParser>();

        new MavLinkV2FrameParser(registry, logger)
            .Parse(packet, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
        logger.Messages.Should().Contain(message =>
            message.Contains("invalid CRC", StringComparison.Ordinal)
            && message.Contains("AHRS", StringComparison.Ordinal));
    }

    private static byte[] BuildFrame(
        MavLinkMessageDefinition definition,
        ReadOnlySpan<byte> payload,
        bool signed = false,
        bool mavLink2 = true)
    {
        var headerLength = mavLink2 ? 10 : 6;
        var packet = new byte[headerLength + payload.Length + 2 + (signed ? 13 : 0)];
        packet[0] = mavLink2 ? (byte)0xfd : (byte)0xfe;
        packet[1] = checked((byte)payload.Length);
        if (mavLink2)
        {
            packet[2] = signed ? (byte)1 : (byte)0;
            packet[4] = 7;
            packet[5] = 1;
            packet[6] = 1;
            packet[7] = (byte)definition.MessageId;
            packet[8] = (byte)(definition.MessageId >> 8);
            packet[9] = (byte)(definition.MessageId >> 16);
        }
        else
        {
            packet[2] = 7;
            packet[3] = 1;
            packet[4] = 1;
            packet[5] = checked((byte)definition.MessageId);
        }

        payload.CopyTo(packet.AsSpan(headerLength));
        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, headerLength - 1 + payload.Length), definition.CrcExtra);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(headerLength + payload.Length), crc);
        return packet;
    }

    private static IReadOnlyList<DialectMessageDefinition> LoadOfficialDefinitions() =>
        MavLinkDialectLoader.Load(Path.Combine(
            RepositoryRoot(),
            "src",
            "Core",
            "MissionPlanner.MavLink",
            "Dialects",
            "ardupilotmega.xml"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "MissionPlanner.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the MissionPlanner repository root.");
    }

    private sealed class SingleDefinitionRegistry(MavLinkMessageDefinition definition)
        : IMavLinkMessageDefinitionRegistry
    {
        public IReadOnlyCollection<MavLinkMessageDefinition> Definitions { get; } = [definition];

        public bool TryGet(
            uint messageId,
            [NotNullWhen(true)] out MavLinkMessageDefinition? result)
        {
            result = messageId == definition.MessageId ? definition : null;
            return result is not null;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
