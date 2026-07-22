using System.Buffers.Binary;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Generator;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates generated MAVLink wire models and decoders against the pinned XML schemas.
/// </summary>
public sealed class MavLinkGeneratedWireModelTests
{
    private const string SourceRevision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";
    private static readonly TransportEndPoint TestEndPoint = new("test");

    /// <summary>
    /// Verifies every selected non-deprecated schema is covered by a generated decoder or documented override.
    /// </summary>
    [Fact]
    public void EverySelectedNonDeprecatedMessageHasTypedCoverage()
    {
        var schemas = LoadSchemas();
        var registry = new MavLinkMessageDefinitionRegistry();
        var generated = GeneratedMavLinkMessageDecoderCatalog.Create(registry);
        var expectedGenerated = schemas.Where(IsGenerated).ToArray();

        generated.Should().HaveCount(expectedGenerated.Length);
        generated.Select(decoder => decoder.MessageId).Should().OnlyHaveUniqueItems();
        generated.Select(decoder => decoder.MessageId)
            .Should().BeEquivalentTo(expectedGenerated.Select(schema => schema.MessageId));

        foreach (var decoder in generated)
        {
            registry.TryGet(decoder.MessageId, out var definition).Should().BeTrue();
            decoder.CrcExtra.Should().Be(definition!.CrcExtra);
        }

        schemas.Where(schema => !schema.IsDeprecated)
            .Should().OnlyContain(schema =>
                IsGenerated(schema) || MavLinkWireModelSourceGenerator.HandWrittenOverrides.Contains(schema.Name));
    }

    /// <summary>
    /// Verifies every generated decoder handles minimum, maximum, and every extension-truncation length losslessly.
    /// </summary>
    [Fact]
    public void GeneratedDecodersHandleAllValidPayloadLengths()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var decoders = GeneratedMavLinkMessageDecoderCatalog.Create(registry).ToDictionary(decoder => decoder.MessageId);

        foreach (var schema in LoadSchemas().Where(IsGenerated))
        {
            var decoder = decoders[schema.MessageId];
            for (var length = (int)schema.MinimumPayloadLength; length <= schema.MaximumPayloadLength; length++)
            {
                var payload = Enumerable.Repeat((byte)0x41, length).ToArray();
                decoder.TryDecode(CreateFrame(schema.MessageId, payload), out var decoded)
                    .Should().BeTrue($"{schema.Name} length {length} is valid");
                var generated = decoded.Should().BeAssignableTo<GeneratedMavLinkMessage>().Subject;
                var roundTrip = generated.EncodePayload(truncateExtensions: false);
                roundTrip[..length].Should().Equal(payload, $"{schema.Name} must preserve supplied bytes");
                if (length < roundTrip.Length)
                {
                    roundTrip[length..].Should().OnlyContain(value => value == 0, $"{schema.Name} omitted extension bytes default to zero");
                }
            }
        }
    }

    /// <summary>
    /// Verifies malformed lengths are rejected by each generated decoder through registry bounds.
    /// </summary>
    [Fact]
    public void GeneratedDecodersRejectMalformedPayloadLengths()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var decoders = GeneratedMavLinkMessageDecoderCatalog.Create(registry).ToDictionary(decoder => decoder.MessageId);

        foreach (var schema in LoadSchemas().Where(IsGenerated))
        {
            var decoder = decoders[schema.MessageId];
            if (schema.MinimumPayloadLength > 0)
            {
                decoder.TryDecode(CreateFrame(schema.MessageId, new byte[schema.MinimumPayloadLength - 1]), out _)
                    .Should().BeFalse($"{schema.Name} is shorter than its registry minimum");
            }

            decoder.TryDecode(CreateFrame(schema.MessageId, new byte[schema.MaximumPayloadLength + 1]), out _)
                .Should().BeFalse($"{schema.Name} is longer than its registry maximum");
        }
    }

    /// <summary>
    /// Verifies exact numeric types, integer boundaries, IEEE special values, arrays, and fixed strings round-trip.
    /// </summary>
    [Fact]
    public void GeneratedModelsPreserveSchemaTypesAndCanonicalBoundaryFixtures()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var decoders = GeneratedMavLinkMessageDecoderCatalog.Create(registry).ToDictionary(decoder => decoder.MessageId);

        foreach (var schema in LoadSchemas().Where(IsGenerated))
        {
            var payload = CreateCanonicalPayload(schema);
            decoders[schema.MessageId].TryDecode(CreateFrame(schema.MessageId, payload), out var decoded).Should().BeTrue();
            var generated = decoded.Should().BeAssignableTo<GeneratedMavLinkMessage>().Subject;
            generated.EncodePayload(truncateExtensions: false).Should().Equal(payload, schema.Name);

            foreach (var field in schema.Fields)
            {
                var property = generated.GetType().GetProperty(GetPropertyName(field));
                property.Should().NotBeNull();
                property!.PropertyType.Should().Be(GetPropertyType(field), $"{schema.Name}.{field.Name} must retain its protocol type");
            }
        }
    }

    /// <summary>
    /// Verifies the requested high-value families resolve to their intended typed wire models.
    /// </summary>
    [Fact]
    public void HighValueMessagesHaveExplicitTypedRegressions()
    {
        var expectedTypes = new Dictionary<uint, Type>
        {
            [0] = typeof(HeartbeatMessage),
            [1] = typeof(SysStatusMessage),
            [24] = typeof(GpsRawIntMessage),
            [30] = typeof(AttitudeMessage),
            [31] = typeof(AttitudeQuaternionMessage),
            [32] = typeof(LocalPositionNedMessage),
            [33] = typeof(GlobalPositionIntMessage),
            [36] = typeof(ServoOutputRawMessage),
            [65] = typeof(RcChannelsMessage),
            [74] = typeof(VfrHudMessage),
            [109] = typeof(RadioStatusMessage),
            [124] = typeof(Gps2RawMessage),
            [125] = typeof(PowerStatusMessage),
            [147] = typeof(BatteryStatusMessage),
            [148] = typeof(AutopilotVersionMessage),
            [163] = typeof(AhrsMessage),
            [165] = typeof(HwstatusMessage),
            [166] = typeof(RadioMessage),
            [168] = typeof(WindMessage),
            [178] = typeof(Ahrs2Message),
            [181] = typeof(Battery2Message),
            [182] = typeof(Ahrs3Message),
            [231] = typeof(WindCovMessage),
            [241] = typeof(VibrationMessage),
            [242] = typeof(HomePositionMessage),
            [245] = typeof(ExtendedSysStateMessage),
            [253] = typeof(StatusTextMessage),
            [264] = typeof(FlightInformationMessage),
            [11030] = typeof(EscTelemetry1To4Message),
            [11031] = typeof(EscTelemetry5To8Message),
            [11032] = typeof(EscTelemetry9To12Message),
            [11040] = typeof(EscTelemetry13To16Message),
            [11041] = typeof(EscTelemetry17To20Message),
            [11042] = typeof(EscTelemetry21To24Message),
            [11043] = typeof(EscTelemetry25To28Message),
            [11044] = typeof(EscTelemetry29To32Message)
        };
        using var provider = CreateServices();
        var decoders = provider.GetRequiredService<MavLinkMessageDecoders>();
        var schemas = LoadSchemas().ToDictionary(schema => schema.MessageId);

        foreach (var expected in expectedTypes)
        {
            var schema = schemas[expected.Key];
            decoders.TryDecode(CreateFrame(schema.MessageId, new byte[schema.MaximumPayloadLength]), out var decoded)
                .Should().BeTrue(schema.Name);
            decoded.Should().BeOfType(expected.Value, schema.Name);
        }
    }

    /// <summary>
    /// Verifies all documented hand-written overrides still decode according to the generated schema metadata.
    /// </summary>
    [Fact]
    public void HandWrittenOverridesMatchGeneratedSchemas()
    {
        using var provider = CreateServices();
        var decoders = provider.GetRequiredService<MavLinkMessageDecoders>();
        var schemas = LoadSchemas().Where(schema => MavLinkWireModelSourceGenerator.HandWrittenOverrides.Contains(schema.Name)).ToArray();

        schemas.Select(schema => schema.Name)
            .Should().BeEquivalentTo(MavLinkWireModelSourceGenerator.HandWrittenOverrides);
        foreach (var schema in schemas)
        {
            decoders.TryDecode(CreateFrame(schema.MessageId, new byte[schema.MaximumPayloadLength]), out var decoded)
                .Should().BeTrue(schema.Name);
            decoded.Should().NotBeOfType<RawMavLinkMessage>(schema.Name);
            decoded!.MessageId.Should().Be(schema.MessageId);
        }
    }

    /// <summary>
    /// Verifies generated wire sources are deterministic and match their committed files.
    /// </summary>
    [Fact]
    public void GeneratedWireSourcesAreDeterministic()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schemas = LoadSchemas();
        var models = MavLinkWireModelSourceGenerator.GenerateModels(schemas, SourceRevision);
        var decoders = MavLinkWireModelSourceGenerator.GenerateDecoders(schemas, SourceRevision);

        models.Should().Be(File.ReadAllText(Path.Combine(repositoryRoot, "src", "Core", "MissionPlanner.MavLink", "Generated", "MavLinkWireMessages.g.cs")));
        decoders.Should().Be(File.ReadAllText(Path.Combine(repositoryRoot, "src", "Core", "MissionPlanner.MavLink", "Generated", "MavLinkWireDecoders.g.cs")));
    }

    /// <summary>
    /// Verifies the committed generation manifest matches generator ownership and vendored inputs.
    /// </summary>
    [Fact]
    public void GenerationManifestDeclaresEveryInputAndOverride()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dialectDirectory = Path.Combine(repositoryRoot, "src", "Core", "MissionPlanner.MavLink", "Dialects");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dialectDirectory, "mavlink-generation.json")));
        var root = document.RootElement;

        root.GetProperty("sourceRevision").GetString().Should().Be(SourceRevision);
        root.GetProperty("rootDialect").GetString().Should().Be("ardupilotmega.xml");
        var inherited = root.GetProperty("inheritedDialects").EnumerateArray().Select(item => item.GetString()!).ToArray();
        inherited.Should().BeEquivalentTo(
        [
            "common.xml", "standard.xml", "minimal.xml", "uAvionix.xml", "icarous.xml",
            "loweheiser.xml", "cubepilot.xml", "csAirLink.xml"
        ]);
        inherited.Append("ardupilotmega.xml").Should().OnlyContain(name => File.Exists(Path.Combine(dialectDirectory, name)));
        root.GetProperty("handWrittenOverrides").EnumerateArray().Select(item => item.GetString()!)
            .Should().BeEquivalentTo(MavLinkWireModelSourceGenerator.HandWrittenOverrides);
        root.GetProperty("deprecatedGeneratedExceptions").EnumerateArray().Select(item => item.GetString()!)
            .Should().BeEquivalentTo(MavLinkWireModelSourceGenerator.DeprecatedCompatibilityMessages);
        root.GetProperty("knownLegacyConstants").EnumerateArray().Select(item => item.GetString()!)
            .Should().Equal("MissionChanged=52");
        root.GetProperty("domainPromotionCatalog").GetString().Should().Be("docs/mavlink-promotion-catalog.json");
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddMavLinkServices(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private static IReadOnlyList<DialectWireMessageDefinition> LoadSchemas()
    {
        var repositoryRoot = FindRepositoryRoot();
        return MavLinkDialectWireLoader.Load(Path.Combine(
            repositoryRoot,
            "src",
            "Core",
            "MissionPlanner.MavLink",
            "Dialects",
            "ardupilotmega.xml"));
    }

    private static bool IsGenerated(DialectWireMessageDefinition schema) =>
        (!schema.IsDeprecated || schema.Name is "BATTERY2" or "HWSTATUS" or "MISSION_ITEM" or "MISSION_REQUEST")
        && !MavLinkWireModelSourceGenerator.HandWrittenOverrides.Contains(schema.Name);

    private static MavLinkFrame CreateFrame(uint messageId, byte[] payload) =>
        new(1, 2, TestEndPoint, messageId, 3, payload, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UnixEpoch);

    private static byte[] CreateCanonicalPayload(DialectWireMessageDefinition schema)
    {
        var payload = new byte[schema.MaximumPayloadLength];
        foreach (var field in schema.Fields)
        {
            if (field.DeclaredType == "char" && field.ArrayLength > 1)
            {
                var count = Math.Min(field.ArrayLength - 1, 4);
                for (var index = 0; index < count; index++)
                {
                    payload[field.WireOffset + index] = (byte)('A' + ((field.SourceIndex + index) % 26));
                }

                continue;
            }

            for (var index = 0; index < field.ArrayLength; index++)
            {
                WriteBoundaryValue(payload, field, index);
            }
        }

        return payload;
    }

    private static void WriteBoundaryValue(Span<byte> payload, DialectWireFieldDefinition field, int index)
    {
        var offset = field.WireOffset + (index * field.ElementSize);
        var alternate = ((field.SourceIndex + index) & 1) == 0;
        switch (field.DeclaredType)
        {
            case "char":
            case "uint8_t":
            case "uint8_t_mavlink_version":
                payload[offset] = alternate ? byte.MaxValue : byte.MinValue;
                break;
            case "int8_t":
                payload[offset] = unchecked((byte)(alternate ? sbyte.MinValue : sbyte.MaxValue));
                break;
            case "uint16_t":
                BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], alternate ? ushort.MaxValue : ushort.MinValue);
                break;
            case "int16_t":
                BinaryPrimitives.WriteInt16LittleEndian(payload[offset..], alternate ? short.MinValue : short.MaxValue);
                break;
            case "uint32_t":
                BinaryPrimitives.WriteUInt32LittleEndian(payload[offset..], alternate ? uint.MaxValue : uint.MinValue);
                break;
            case "int32_t":
                BinaryPrimitives.WriteInt32LittleEndian(payload[offset..], alternate ? int.MinValue : int.MaxValue);
                break;
            case "uint64_t":
                BinaryPrimitives.WriteUInt64LittleEndian(payload[offset..], alternate ? ulong.MaxValue : ulong.MinValue);
                break;
            case "int64_t":
                BinaryPrimitives.WriteInt64LittleEndian(payload[offset..], alternate ? long.MinValue : long.MaxValue);
                break;
            case "float":
                BinaryPrimitives.WriteInt32LittleEndian(payload[offset..], alternate ? unchecked((int)0x7fc00001) : unchecked((int)0x80000000));
                break;
            case "double":
                BinaryPrimitives.WriteInt64LittleEndian(payload[offset..], alternate ? unchecked((long)0x7ff8000000000001) : long.MinValue);
                break;
            default:
                throw new InvalidDataException($"Unsupported field type {field.DeclaredType}.");
        }
    }

    private static string GetPropertyName(DialectWireFieldDefinition field)
    {
        var name = MavLinkEnumSourceGenerator.ToIdentifier(field.Name);
        return name == "MessageId" ? "PayloadMessageId" : name;
    }

    private static Type GetPropertyType(DialectWireFieldDefinition field)
    {
        if (field.DeclaredType == "char" && field.ArrayLength > 1)
        {
            return typeof(string);
        }

        var scalar = field.DeclaredType switch
        {
            "char" or "uint8_t" or "uint8_t_mavlink_version" => typeof(byte),
            "int8_t" => typeof(sbyte),
            "uint16_t" => typeof(ushort),
            "int16_t" => typeof(short),
            "uint32_t" => typeof(uint),
            "int32_t" => typeof(int),
            "uint64_t" => typeof(ulong),
            "int64_t" => typeof(long),
            "float" => typeof(float),
            "double" => typeof(double),
            _ => throw new InvalidDataException($"Unsupported field type {field.DeclaredType}.")
        };
        return field.ArrayLength > 1 ? scalar.MakeArrayType() : scalar;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "tasks", "mavlink-codex-tasks")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the MissionPlanner repository root.");
    }
}
