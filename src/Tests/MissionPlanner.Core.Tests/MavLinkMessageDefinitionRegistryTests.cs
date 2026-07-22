using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Generator;
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

    /// <summary>Verifies MAVLink 2 extension bytes may be omitted or partially truncated.</summary>
    [Fact]
    public void ExtensionPayloadLengthsBetweenMinimumAndMaximumAreAccepted()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(24, out var gpsRawInt).Should().BeTrue();
        var lengths = new[]
        {
            gpsRawInt!.MinimumPayloadLength,
            checked((byte)(gpsRawInt.MinimumPayloadLength + 1)),
            gpsRawInt.MaximumPayloadLength
        };

        foreach (var length in lengths)
        {
            var packet = BuildFrame(gpsRawInt, new byte[length]);
            var frames = new MavLinkV2FrameParser(registry).Parse(packet, TestEndPoint, DateTimeOffset.UtcNow);
            frames.Should().ContainSingle().Which.Payload.Length.Should().Be(length);
        }
    }

    /// <summary>Verifies payloads outside the official wire-length window are rejected.</summary>
    [Fact]
    public void PayloadLengthsOutsideDefinitionWindowAreRejected()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(24, out var gpsRawInt).Should().BeTrue();
        var belowMinimum = BuildFrame(gpsRawInt!, new byte[gpsRawInt!.MinimumPayloadLength - 1]);
        var aboveMaximum = BuildFrame(gpsRawInt, new byte[gpsRawInt.MaximumPayloadLength + 1]);
        var logger = new RecordingLogger<MavLinkV2FrameParser>();

        new MavLinkV2FrameParser(registry, logger)
            .Parse(belowMinimum, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
        new MavLinkV2FrameParser(registry, logger)
            .Parse(aboveMaximum, TestEndPoint, DateTimeOffset.UtcNow)
            .Should().BeEmpty();
        logger.Messages.Should().Contain(message =>
            message.Contains("GPS_RAW_INT", StringComparison.Ordinal)
            && message.Contains("MinimumPayloadLength=30", StringComparison.Ordinal)
            && message.Contains("MaximumPayloadLength=52", StringComparison.Ordinal));
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
        bool signed = false)
    {
        var packet = new byte[10 + payload.Length + 2 + (signed ? 13 : 0)];
        packet[0] = 0xfd;
        packet[1] = checked((byte)payload.Length);
        packet[2] = signed ? (byte)1 : (byte)0;
        packet[4] = 7;
        packet[5] = 1;
        packet[6] = 1;
        packet[7] = (byte)definition.MessageId;
        packet[8] = (byte)(definition.MessageId >> 8);
        packet[9] = (byte)(definition.MessageId >> 16);
        payload.CopyTo(packet.AsSpan(10));
        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, 9 + payload.Length), definition.CrcExtra);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10 + payload.Length), crc);
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
