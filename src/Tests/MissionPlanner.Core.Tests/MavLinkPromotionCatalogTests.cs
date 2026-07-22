using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Generator;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Enforces the MAVLink domain-promotion policy and single-owner catalog.
/// </summary>
public sealed class MavLinkPromotionCatalogTests
{
    private const string SourceRevision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";
    private static readonly TransportEndPoint TestEndPoint = new("test");

    /// <summary>
    /// Verifies every selected-dialect message has one deterministic machine-readable catalog entry.
    /// </summary>
    [Fact]
    public void CatalogIsCompleteUniqueAndDeterministic()
    {
        var repositoryRoot = FindRepositoryRoot();
        var definitions = LoadDefinitions(repositoryRoot);
        var path = Path.Combine(repositoryRoot, "docs", "mavlink-promotion-catalog.json");
        var committed = File.ReadAllText(path);
        var catalog = Deserialize(committed);

        catalog.SourceRevision.Should().Be(SourceRevision);
        catalog.Messages.Should().HaveCount(definitions.Count);
        catalog.Messages.Select(entry => entry.MessageId).Should().OnlyHaveUniqueItems();
        catalog.Messages.Select(entry => entry.Name).Should().OnlyHaveUniqueItems();
        catalog.Messages.Select(entry => (entry.MessageId, entry.Name))
            .Should().Equal(definitions.Select(definition => (definition.MessageId, definition.Name)));
        catalog.Messages.Should().OnlyContain(entry =>
            !string.IsNullOrWhiteSpace(entry.Owner)
            && !string.IsNullOrWhiteSpace(entry.IntendedUpdateFrequency)
            && entry.UiConsumers.Count > 0);

        MavLinkPromotionCatalogGenerator.Generate(definitions, SourceRevision).Should().Be(committed);
    }

    /// <summary>
    /// Verifies every general domain-handler message has exactly one catalog owner naming that handler.
    /// </summary>
    [Fact]
    public void EveryDomainHandlerReferencesSingleOwnedCatalogEntries()
    {
        var catalog = LoadCatalog().Messages.ToDictionary(entry => entry.MessageId);
        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoderCatalog = new MavLinkMessageDecoderCatalog(definitions);
        var messageIdsByType = CreateMessageTypeMap(decoderCatalog);
        var handlers = CreateHandlers();
        var ownerByMessageType = new Dictionary<Type, Type>();

        foreach (var handler in handlers)
        {
            foreach (var messageType in handler.MessageTypes)
            {
                ownerByMessageType.TryAdd(messageType, handler.GetType()).Should().BeTrue(
                    $"{messageType.Name} must not have multiple general domain handlers");
                messageIdsByType.TryGetValue(messageType, out var messageId).Should().BeTrue(messageType.Name);
                var entry = catalog[messageId];
                entry.Owner.Should().Contain(handler.GetType().Name);
                entry.Category.Should().BeOneOf(
                    MavLinkPromotionCategory.VehicleStateTelemetry,
                    MavLinkPromotionCategory.ProtocolWorkflow,
                    MavLinkPromotionCategory.DomainEvent);
            }
        }
    }

    /// <summary>
    /// Verifies every promoted observation or model named by the catalog exists in the Core domain.
    /// </summary>
    [Fact]
    public void EveryPromotedObservationHasAnApplicationContract()
    {
        var coreTypes = typeof(VehicleSession).Assembly.GetTypes().ToLookup(type => type.Name, StringComparer.Ordinal);
        var promoted = LoadCatalog().Messages.Where(entry => entry.ObservationType is not null).ToArray();

        promoted.Should().NotBeEmpty();
        foreach (var entry in promoted)
        {
            var matches = coreTypes[entry.ObservationType!].ToArray();
            matches.Should().ContainSingle($"{entry.Name} names {entry.ObservationType}");
            if (entry.ObservationType!.EndsWith("Observation", StringComparison.Ordinal))
            {
                matches[0].Should().Implement<IVehicleObservation>();
            }
        }
    }

    /// <summary>
    /// Verifies diagnostic messages remain available as lossless raw envelopes without domain promotion.
    /// </summary>
    [Fact]
    public void DiagnosticMessageRemainsAvailableToRawInspection()
    {
        var entry = LoadCatalog().Messages.Single(item => item.Name == "NAMED_VALUE_FLOAT");
        entry.Category.Should().Be(MavLinkPromotionCategory.DiagnosticRawTelemetry);
        var registry = new MavLinkMessageDefinitionRegistry();
        registry.TryGet(entry.MessageId, out var definition).Should().BeTrue();
        var payload = Enumerable.Range(1, definition!.MaximumPayloadLength).Select(value => (byte)value).ToArray();
        var frame = new MavLinkFrame(1, 2, TestEndPoint, definition.MessageId, 7, payload, payload, DateTimeOffset.UnixEpoch);

        var raw = new RawMavLinkMessageDecoder(registry).Decode(frame);

        raw.RawMessageId.Should().Be(entry.MessageId);
        raw.MessageName.Should().Be(entry.Name);
        raw.Payload.Should().Equal(payload);
    }

    private static IReadOnlyDictionary<Type, uint> CreateMessageTypeMap(MavLinkMessageDecoderCatalog catalog)
    {
        var result = new Dictionary<Type, uint>();
        foreach (var entry in catalog.Entries)
        {
            var payload = new byte[entry.Definition.MaximumPayloadLength];
            var frame = new MavLinkFrame(1, 2, TestEndPoint, entry.Definition.MessageId, 0, payload, payload, DateTimeOffset.UnixEpoch);
            entry.Decoder.TryDecode(frame, out var message).Should().BeTrue(entry.Definition.Name);
            result.TryAdd(message!.GetType(), entry.Definition.MessageId).Should().BeTrue(entry.Definition.Name);
        }

        return result;
    }

    private static IReadOnlyList<IVehicleMessageHandler> CreateHandlers()
    {
        Type[] handlerTypes =
        [
            typeof(FlightTelemetryHandler),
            typeof(NavigationTelemetryHandler),
            typeof(PowerTelemetryHandler),
            typeof(RadioTelemetryHandler),
            typeof(HealthTelemetryHandler),
            typeof(SensorTelemetryHandler),
            typeof(ControlMessageHandler)
        ];
        return handlerTypes.Select(type =>
        {
            type.GetConstructors().Should().ContainSingle();
            var constructor = type.GetConstructors().Single();
            return (IVehicleMessageHandler)constructor.Invoke(new object?[constructor.GetParameters().Length]);
        }).ToArray();
    }

    private static MavLinkPromotionCatalogDocument LoadCatalog()
    {
        var path = Path.Combine(FindRepositoryRoot(), "docs", "mavlink-promotion-catalog.json");
        return Deserialize(File.ReadAllText(path));
    }

    private static MavLinkPromotionCatalogDocument Deserialize(string json)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<MavLinkPromotionCatalogDocument>(json, options)
            ?? throw new InvalidDataException("The MAVLink promotion catalog is empty.");
    }

    private static IReadOnlyList<DialectMessageDefinition> LoadDefinitions(string repositoryRoot) =>
        MavLinkDialectLoader.Load(Path.Combine(
            repositoryRoot,
            "src",
            "Core",
            "MissionPlanner.MavLink",
            "Dialects",
            "ardupilotmega.xml"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "MissionPlanner.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the MissionPlanner repository root.");
    }
}
