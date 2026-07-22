using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates deterministic decoder registration, DI resolution, and startup failures.
/// </summary>
public sealed class MavLinkDecoderCatalogTests
{
    private static readonly TransportEndPoint TestEndPoint = new("test");

    /// <summary>
    /// Verifies the application provider resolves one complete validated decoder catalog.
    /// </summary>
    [Fact]
    public void ApplicationProviderResolvesCompleteCatalog()
    {
        using var provider = CreateServices();
        var catalog = provider.GetRequiredService<IMavLinkMessageDecoderCatalog>();

        catalog.Entries.Should().HaveCount(GeneratedMavLinkMessageDecoderCatalog.Schemas.Count);
        catalog.Decoders.Should().HaveSameCount(catalog.Entries);
        catalog.Decoders.Select(decoder => decoder.MessageId).Should().OnlyHaveUniqueItems();
        provider.GetRequiredService<MavLinkMessageDecoders>().Should().NotBeNull();
    }

    /// <summary>
    /// Verifies every generated typed schema has exactly one generated decoder.
    /// </summary>
    [Fact]
    public void EveryGeneratedTypedMessageHasOneDecoder()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var generated = GeneratedMavLinkMessageDecoderCatalog.Create(registry);
        var expected = GeneratedMavLinkMessageDecoderCatalog.Schemas
            .Where(schema => schema.Kind == MavLinkDecoderKind.Generated)
            .Select(schema => schema.MessageId);

        generated.Select(decoder => decoder.MessageId).Should().Equal(expected);
        generated.Select(decoder => decoder.MessageId).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Verifies every declared override occupies exactly one typed schema slot and no generated slot.
    /// </summary>
    [Fact]
    public void EveryOverrideReplacesExactlyOneGeneratedSlot()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var generatedIds = GeneratedMavLinkMessageDecoderCatalog.Create(registry).Select(decoder => decoder.MessageId).ToHashSet();
        var custom = GeneratedMavLinkMessageDecoderCatalog.CreateDeclaredCustomDecoders();
        var customSchemas = GeneratedMavLinkMessageDecoderCatalog.Schemas
            .Where(schema => schema.Kind is MavLinkDecoderKind.HandWrittenOverride or MavLinkDecoderKind.ProtocolWorkflow)
            .ToArray();

        custom.Should().HaveCount(customSchemas.Length);
        custom.Select(decoder => decoder.MessageId).Should().Equal(customSchemas.Select(schema => schema.MessageId));
        custom.Select(decoder => decoder.MessageId).Should().OnlyHaveUniqueItems();
        custom.Should().OnlyContain(decoder => !generatedIds.Contains(decoder.MessageId));
    }

    /// <summary>
    /// Verifies duplicate effective IDs fail with a deterministic startup error.
    /// </summary>
    [Fact]
    public void DuplicateRegistrationFailsDeterministically()
    {
        var act = () => new MavLinkMessageDecoderCatalog(
            new MavLinkMessageDefinitionRegistry(),
            [new HeartbeatMessageDecoder()]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate typed decoder registration*MAVLink message ID 0*");
    }

    /// <summary>
    /// Verifies a typed schema whose ID is absent from the registry fails before catalog use.
    /// </summary>
    [Fact]
    public void MissingRegistryIdFailsFast()
    {
        var definitions = new MavLinkMessageDefinitionRegistry().Definitions.Where(definition => definition.MessageId != 2);
        var act = () => new MavLinkMessageDecoderCatalog(new TestRegistry(definitions));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*message ID 2 is missing from the selected-dialect registry*");
    }

    /// <summary>
    /// Verifies stale generated payload metadata fails before any decoder is dispatched.
    /// </summary>
    [Fact]
    public void IncompatiblePayloadMetadataFailsFast()
    {
        var definitions = new MavLinkMessageDefinitionRegistry().Definitions
            .Select(definition => definition.MessageId == 2
                ? definition with { MinimumPayloadLength = 11 }
                : definition);
        var act = () => new MavLinkMessageDecoderCatalog(new TestRegistry(definitions));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SYSTEM_TIME (2) is incompatible with registry metadata*");
    }

    /// <summary>
    /// Verifies protocol-specific MAVFTP and parameter decoders remain effective through the catalog.
    /// </summary>
    [Fact]
    public void ProtocolSpecificDecodersRemainFunctional()
    {
        using var provider = CreateServices();
        var facade = provider.GetRequiredService<MavLinkMessageDecoders>();
        var registry = provider.GetRequiredService<IMavLinkMessageDefinitionRegistry>();

        registry.TryGet(110, out var ftp).Should().BeTrue();
        facade.TryDecode(CreateFrame(ftp!, new byte[ftp!.MaximumPayloadLength]), out var ftpMessage).Should().BeTrue();
        ftpMessage.Should().BeOfType<FileTransferProtocolMessage>();

        registry.TryGet(22, out var parameter).Should().BeTrue();
        facade.TryDecode(CreateFrame(parameter!, new byte[parameter!.MaximumPayloadLength]), out var parameterMessage).Should().BeTrue();
        parameterMessage.Should().BeOfType<ParamValueMessage>();
    }

    /// <summary>
    /// Verifies tests can still construct isolated decoder sets without application DI.
    /// </summary>
    [Fact]
    public void ManuallyConstructedDecoderSetsRemainIsolated()
    {
        var registry = new MavLinkMessageDefinitionRegistry();
        var facade = new MavLinkMessageDecoders([new HeartbeatMessageDecoder()], registry);
        registry.TryGet(0, out var heartbeat).Should().BeTrue();
        registry.TryGet(168, out var wind).Should().BeTrue();

        facade.TryDecode(CreateFrame(heartbeat!, new byte[heartbeat!.MaximumPayloadLength]), out var typed).Should().BeTrue();
        typed.Should().BeOfType<HeartbeatMessage>();
        facade.TryDecode(CreateFrame(wind!, new byte[wind!.MaximumPayloadLength]), out var raw).Should().BeTrue();
        raw.Should().BeOfType<RawMavLinkMessage>();
    }

    private static MavLinkFrame CreateFrame(MavLinkMessageDefinition definition, byte[] payload) =>
        new(1, 2, TestEndPoint, definition.MessageId, 3, payload, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UnixEpoch);

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddMavLinkServices(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private sealed class TestRegistry(IEnumerable<MavLinkMessageDefinition> definitions) : IMavLinkMessageDefinitionRegistry
    {
        private readonly IReadOnlyDictionary<uint, MavLinkMessageDefinition> definitionsById =
            definitions.ToDictionary(definition => definition.MessageId);

        public IReadOnlyCollection<MavLinkMessageDefinition> Definitions => definitionsById.Values.ToArray();

        public bool TryGet(
            uint messageId,
            [NotNullWhen(true)] out MavLinkMessageDefinition? definition) =>
            definitionsById.TryGetValue(messageId, out definition);
    }
}
