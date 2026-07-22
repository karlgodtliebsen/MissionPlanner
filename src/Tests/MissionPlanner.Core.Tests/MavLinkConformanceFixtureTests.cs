using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Generator;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates the generated MAVLink implementation against committed pymavlink vectors.
/// </summary>
public sealed class MavLinkConformanceFixtureTests
{
    private const string SourceRevision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";
    private static readonly TransportEndPoint EndPoint = new("conformance");
    private static readonly DateTimeOffset ReceivedAt = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Verifies every generated decoder against independent minimum and maximum pymavlink frames.
    /// </summary>
    [Fact]
    public void EveryGeneratedDecoderMatchesIndependentPymavlinkVectors()
    {
        var artifact = LoadArtifact();
        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoders = GeneratedMavLinkMessageDecoderCatalog.Create(definitions).ToDictionary(item => item.MessageId);
        var schemas = LoadSchemas().ToDictionary(item => item.Name);

        Assert.Equal(SourceRevision, artifact.SourceRevision);
        Assert.Equal("ardupilotmega.xml", artifact.RootDialect);
        Assert.Equal("pymavlink", artifact.Generator);
        Assert.Equal(decoders.Keys.Order(), artifact.Fixtures.Select(item => item.MessageId).Order());

        foreach (var fixture in artifact.Fixtures)
        {
            var definition = Assert.Single(definitions.Definitions, item => item.MessageId == fixture.MessageId);
            Assert.Equal(fixture.Name, definition.Name);
            Assert.Equal(fixture.CrcExtra, definition.CrcExtra);
            Assert.Equal(fixture.MinimumPayloadLength, definition.MinimumPayloadLength);
            Assert.Equal(fixture.MaximumPayloadLength, definition.MaximumPayloadLength);

            foreach (var variant in fixture.Variants)
            {
                var parser = new MavLinkV2FrameParser(definitions);
                var wireBytes = Convert.FromHexString(variant.Mavlink2FrameHex);
                var frame = Assert.Single(parser.Parse(wireBytes, EndPoint, ReceivedAt));
                Assert.Equal(Convert.FromHexString(variant.PayloadHex), frame.Payload.ToArray());
                Assert.Equal(fixture.Sequence, frame.Sequence);
                Assert.Equal(fixture.SystemId, frame.SystemId);
                Assert.Equal(fixture.ComponentId, frame.ComponentId);

                Assert.True(decoders[fixture.MessageId].TryDecode(frame, out var decoded), fixture.Name);
                var generated = Assert.IsAssignableFrom<GeneratedMavLinkMessage>(decoded);
                Assert.Equal(MavLinkEnumSourceGenerator.ToIdentifier(fixture.Name) + "Message", generated.GetType().Name);
                AssertPayloadRoundTrip(variant, fixture.MaximumPayloadLength, generated);
                AssertExpectedFields(schemas[fixture.Name], variant.ExpectedFields, generated);

                if (variant.Mavlink1FrameHex is not null)
                {
                    var mavlink1Bytes = Convert.FromHexString(variant.Mavlink1FrameHex);
                    var mavlink1Frame = Assert.Single(new MavLinkV2FrameParser(definitions).Parse(mavlink1Bytes, EndPoint, ReceivedAt));
                    Assert.Equal(variant.PayloadHex, Convert.ToHexString(mavlink1Frame.Payload.Span).ToLowerInvariant());
                    Assert.True(decoders[fixture.MessageId].TryDecode(mavlink1Frame, out var mavlink1Decoded), fixture.Name);
                    var mavlink1Generated = Assert.IsAssignableFrom<GeneratedMavLinkMessage>(mavlink1Decoded);
                    Assert.Equal(generated.GetType(), mavlink1Generated.GetType());
                    AssertPayloadRoundTrip(variant, fixture.MaximumPayloadLength, mavlink1Generated);
                }
            }
        }
    }

    /// <summary>
    /// Verifies mixed MAVLink streams resynchronize, preserve signatures and sequence wrap, and reset at reconnect.
    /// </summary>
    [Fact]
    public void ParserHandlesChunkingNoiseCorruptionSigningWrapAndReconnect()
    {
        var artifact = LoadArtifact();
        var definitions = new MavLinkMessageDefinitionRegistry();
        var first = Convert.FromHexString(artifact.Fixtures[0].Variants[0].Mavlink2FrameHex);
        var secondFixture = artifact.Fixtures[2];
        var second = Convert.FromHexString(secondFixture.Variants[0].Mavlink2FrameHex);
        var mavlink1Fixture = artifact.Fixtures.First(item => item.Variants[0].Mavlink1FrameHex is not null);
        var mavlink1 = Convert.FromHexString(mavlink1Fixture.Variants[0].Mavlink1FrameHex!);
        var corrupt = first.ToArray();
        corrupt[^1] ^= 0x5A;
        var signed = AddSignature(second, definitions);
        var stream = new byte[] { 0x11, 0x22, 0x33 }
            .Concat(corrupt)
            .Concat(first)
            .Concat(mavlink1)
            .Concat(signed)
            .ToArray();

        var parser = new MavLinkV2FrameParser(definitions);
        var parsed = new List<MavLinkFrame>();
        foreach (var value in stream)
        {
            parsed.AddRange(parser.Parse([value], EndPoint, ReceivedAt));
        }

        Assert.Equal(3, parsed.Count);
        Assert.Equal(first, parsed[0].RawBytes.ToArray());
        Assert.Equal(0xFE, parsed[1].RawBytes.Span[0]);
        Assert.Equal(signed, parsed[2].RawBytes.ToArray());
        Assert.Equal(13, parsed[2].RawBytes.Length - 12 - parsed[2].Payload.Length);

        parser.Parse(first.AsSpan(0, first.Length / 2), EndPoint, ReceivedAt);
        parser.Reset();
        var afterReconnect = Assert.Single(parser.Parse(second, EndPoint, ReceivedAt));
        Assert.Equal(secondFixture.Sequence, afterReconnect.Sequence);

        var wrapFixtures = artifact.Fixtures.Where(item => item.Sequence is 255 or 0).Take(2).ToArray();
        Assert.Equal([255, 0], wrapFixtures.Select(item => (int)item.Sequence).ToArray());
        var wrapStream = wrapFixtures.SelectMany(item => Convert.FromHexString(item.Variants[0].Mavlink2FrameHex)).ToArray();
        var wrapped = new MavLinkV2FrameParser(definitions).Parse(wrapStream, EndPoint, ReceivedAt);
        Assert.Equal([255, 0], wrapped.Select(item => (int)item.Sequence).ToArray());
    }

    /// <summary>
    /// Guards high-rate parsing and generated decoding against excessive time or allocation regressions.
    /// </summary>
    [Fact]
    public void HighRateParsingAndDecodingStayWithinStableRegressionThresholds()
    {
        const int frameCount = 2_000;
        var artifact = LoadArtifact();
        var fixture = artifact.Fixtures.Single(item => item.Name == "HIGHRES_IMU");
        var wireFrame = Convert.FromHexString(fixture.Variants[^1].Mavlink2FrameHex);
        var stream = new byte[wireFrame.Length * frameCount];
        for (var index = 0; index < frameCount; index++)
        {
            wireFrame.CopyTo(stream, index * wireFrame.Length);
        }

        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoder = GeneratedMavLinkMessageDecoderCatalog.Create(definitions).Single(item => item.MessageId == fixture.MessageId);
        _ = new MavLinkV2FrameParser(definitions).Parse(wireFrame, EndPoint, ReceivedAt);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var frames = new MavLinkV2FrameParser(definitions).Parse(stream, EndPoint, ReceivedAt);
        foreach (var frame in frames)
        {
            Assert.True(decoder.TryDecode(frame, out _));
        }

        stopwatch.Stop();
        var bytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / frameCount;
        var framesPerSecond = frameCount / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        Assert.Equal(frameCount, frames.Count);
        Assert.True(bytesPerFrame < 4_096, $"Allocated {bytesPerFrame:N0} bytes per parsed and decoded frame.");
        Assert.True(framesPerSecond > 500, $"Parsed and decoded only {framesPerSecond:N0} frames/second.");
    }

    /// <summary>
    /// Verifies decoded typed messages route to one domain owner while unowned messages remain inspectable.
    /// </summary>
    [Fact]
    public async Task DispatcherRoutesTypedMessagesToTheirSingleOwner()
    {
        var artifact = LoadArtifact();
        var fixture = artifact.Fixtures.Single(item => item.Name == "SYSTEM_TIME");
        var definitions = new MavLinkMessageDefinitionRegistry();
        var frame = Assert.Single(new MavLinkV2FrameParser(definitions).Parse(
            Convert.FromHexString(fixture.Variants[0].Mavlink2FrameHex), EndPoint, ReceivedAt));
        var decoder = GeneratedMavLinkMessageDecoderCatalog.Create(definitions).Single(item => item.MessageId == fixture.MessageId);
        Assert.True(decoder.TryDecode(frame, out var message));
        var handler = new RecordingHandler(message!.GetType());
        var dispatcher = new VehicleMessageDispatcher([handler]);

        Assert.True(await dispatcher.DispatchAsync(message, TestContext.Current.CancellationToken));
        Assert.Same(message, handler.LastMessage);
        var definition = Assert.Single(definitions.Definitions, item => item.MessageId == fixture.MessageId);
        var raw = new RawMavLinkMessage(
            2,
            1,
            1,
            EndPoint,
            fixture.MessageId,
            0,
            0,
            0,
            frame.Payload.ToArray(),
            [],
            frame.RawBytes.ToArray(),
            definition,
            ReceivedAt);
        Assert.False(await dispatcher.DispatchAsync(raw, TestContext.Current.CancellationToken));
    }

    private static void AssertPayloadRoundTrip(ConformanceVariant variant, int maximumPayloadLength, GeneratedMavLinkMessage message)
    {
        var expected = Convert.FromHexString(variant.PayloadHex);
        var actual = message.EncodePayload(truncateExtensions: false);
        Assert.Equal(maximumPayloadLength, actual.Length);
        Assert.Equal(expected, actual[..expected.Length]);
        Assert.All(actual[expected.Length..], value => Assert.Equal(0, value));
    }

    private static void AssertExpectedFields(DialectWireMessageDefinition schema, IReadOnlyDictionary<string, JsonElement> expectedFields, GeneratedMavLinkMessage message)
    {
        foreach (var field in schema.Fields)
        {
            var propertyName = MavLinkEnumSourceGenerator.ToIdentifier(field.Name);
            if (propertyName == "MessageId")
            {
                propertyName = "PayloadMessageId";
            }

            var property = message.GetType().GetProperty(propertyName);
            Assert.NotNull(property);
            AssertJsonValue(expectedFields[field.Name], property.GetValue(message));
        }
    }

    private static void AssertJsonValue(JsonElement expected, object? actual)
    {
        Assert.NotNull(actual);
        if (expected.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.GetString(), actual);
            return;
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            var actualValues = Assert.IsAssignableFrom<IEnumerable>(actual).Cast<object>().ToArray();
            var expectedValues = expected.EnumerateArray().ToArray();
            Assert.Equal(expectedValues.Length, actualValues.Length);
            for (var index = 0; index < expectedValues.Length; index++)
            {
                AssertJsonValue(expectedValues[index], actualValues[index]);
            }

            return;
        }

        if (actual is float single)
        {
            Assert.Equal((float)expected.GetDouble(), single);
        }
        else if (actual is double number)
        {
            Assert.Equal(expected.GetDouble(), number);
        }
        else
        {
            Assert.Equal(expected.GetDecimal(), Convert.ToDecimal(actual, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static byte[] AddSignature(byte[] unsignedFrame, MavLinkMessageDefinitionRegistry definitions)
    {
        var signed = new byte[unsignedFrame.Length + 13];
        unsignedFrame.CopyTo(signed, 0);
        signed[2] |= 0x01;
        var payloadLength = signed[1];
        var messageId = signed[7] | ((uint)signed[8] << 8) | ((uint)signed[9] << 16);
        var definition = Assert.Single(definitions.Definitions, item => item.MessageId == messageId);
        var crc = MavLinkCrc.Calculate(signed.AsSpan(1, 9 + payloadLength), definition.CrcExtra);
        signed[10 + payloadLength] = (byte)crc;
        signed[11 + payloadLength] = (byte)(crc >> 8);
        for (var index = 0; index < 13; index++)
        {
            signed[12 + payloadLength + index] = (byte)(0xA0 + index);
        }

        return signed;
    }

    private static ConformanceArtifact LoadArtifact() =>
        JsonSerializer.Deserialize<ConformanceArtifact>(File.ReadAllText(FixturePath()), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("The MAVLink conformance fixture is empty.");

    private static IReadOnlyList<DialectWireMessageDefinition> LoadSchemas() =>
        MavLinkDialectWireLoader.Load(Path.Combine(RepositoryRoot(), "src", "Core", "MissionPlanner.MavLink", "Dialects", "ardupilotmega.xml"));

    private static string FixturePath() => Path.Combine(
        RepositoryRoot(), "src", "Tests", "MissionPlanner.Core.Tests", "Fixtures", "mavlink-conformance.json");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "MissionPlanner.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class RecordingHandler(Type messageType) : IVehicleMessageHandler
    {
        public IReadOnlyCollection<Type> MessageTypes { get; } = [messageType];

        public MavLinkMessage? LastMessage { get; private set; }

        public ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
        {
            LastMessage = message;
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed record ConformanceArtifact(
    int SchemaVersion,
    string SourceRevision,
    string RootDialect,
    string Generator,
    string GeneratorVersion,
    IReadOnlyList<ConformanceMessageFixture> Fixtures);

internal sealed record ConformanceMessageFixture(
    string Name,
    uint MessageId,
    byte CrcExtra,
    byte Sequence,
    byte SystemId,
    byte ComponentId,
    int MinimumPayloadLength,
    int MaximumPayloadLength,
    IReadOnlyList<ConformanceVariant> Variants);

internal sealed record ConformanceVariant(
    string Kind,
    IReadOnlyDictionary<string, JsonElement> ExpectedFields,
    string PayloadHex,
    string Mavlink2FrameHex,
    string? Mavlink1FrameHex);
