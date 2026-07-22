using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Combines generated and declared custom decoders and validates the complete typed catalog.
/// </summary>
public sealed class MavLinkMessageDecoderCatalog : IMavLinkMessageDecoderCatalog
{
    /// <summary>
    /// Initializes and validates the default selected-dialect decoder catalog.
    /// </summary>
    /// <param name="definitions">The central selected-dialect message registry.</param>
    public MavLinkMessageDecoderCatalog(IMavLinkMessageDefinitionRegistry definitions)
        : this(definitions, [])
    {
    }

    /// <summary>
    /// Initializes the default catalog with additional registrations and validates the combined set.
    /// </summary>
    /// <param name="definitions">The central selected-dialect message registry.</param>
    /// <param name="additionalDecoders">Additional decoder registrations to validate.</param>
    /// <remarks>
    /// The selected dialect already has complete typed coverage, so an additional decoder must either
    /// conflict with an effective ID or refer to an unregistered ID. This overload exists to make such
    /// startup configuration errors deterministic and directly testable.
    /// </remarks>
    public MavLinkMessageDecoderCatalog(
        IMavLinkMessageDefinitionRegistry definitions,
        IEnumerable<IMavLinkMessageDecoder> additionalDecoders)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(additionalDecoders);

        var schemas = GeneratedMavLinkMessageDecoderCatalog.Schemas;
        var schemasById = ToUniqueDictionary(schemas, schema => schema.MessageId, "decoder schema");
        ValidateSchemaMetadata(definitions, schemas);

        var generated = GeneratedMavLinkMessageDecoderCatalog.Create(definitions);
        var custom = GeneratedMavLinkMessageDecoderCatalog.CreateDeclaredCustomDecoders();
        ValidateDeclaredSet(generated, schemasById, MavLinkDecoderKind.Generated, "generated");
        ValidateDeclaredCustomSet(custom, schemasById);

        var candidates = generated.Concat(custom).Concat(additionalDecoders).ToArray();
        var decodersById = ToUniqueDictionary(candidates, decoder => decoder.MessageId, "typed decoder");
        foreach (var decoder in candidates)
        {
            if (!definitions.TryGet(decoder.MessageId, out var definition))
            {
                throw new InvalidOperationException($"Typed decoder {decoder.GetType().Name} uses message ID {decoder.MessageId}, which is missing from the selected-dialect registry.");
            }

            if (decoder.CrcExtra != definition.CrcExtra)
            {
                throw new InvalidOperationException($"Typed decoder {decoder.GetType().Name} has CRC extra {decoder.CrcExtra}, but registry message {definition.Name} ({definition.MessageId}) requires {definition.CrcExtra}.");
            }
        }

        var missingIds = schemasById.Keys.Except(decodersById.Keys).Order().ToArray();
        if (missingIds.Length > 0)
        {
            throw new InvalidOperationException($"Generated typed message schemas have no effective decoder: {string.Join(", ", missingIds)}.");
        }

        var unexpectedIds = decodersById.Keys.Except(schemasById.Keys).Order().ToArray();
        if (unexpectedIds.Length > 0)
        {
            throw new InvalidOperationException($"Typed decoders are not declared by the generated catalog: {string.Join(", ", unexpectedIds)}.");
        }

        Entries = schemas.OrderBy(schema => schema.MessageId)
            .Select(schema =>
            {
                definitions.TryGet(schema.MessageId, out var definition);
                return new MavLinkDecoderCatalogEntry(definition!, decodersById[schema.MessageId], schema.Kind);
            })
            .ToArray();
        Decoders = Entries.Select(entry => entry.Decoder).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<MavLinkDecoderCatalogEntry> Entries { get; }

    /// <inheritdoc />
    public IReadOnlyList<IMavLinkMessageDecoder> Decoders { get; }

    private static Dictionary<uint, T> ToUniqueDictionary<T>(
        IEnumerable<T> values,
        Func<T, uint> selectId,
        string registrationName)
    {
        var result = new Dictionary<uint, T>();
        foreach (var value in values)
        {
            var id = selectId(value);
            if (!result.TryAdd(id, value))
            {
                throw new InvalidOperationException($"Duplicate {registrationName} registration for MAVLink message ID {id}.");
            }
        }

        return result;
    }

    private static void ValidateSchemaMetadata(
        IMavLinkMessageDefinitionRegistry definitions,
        IEnumerable<MavLinkDecoderCatalogSchema> schemas)
    {
        foreach (var schema in schemas)
        {
            if (!definitions.TryGet(schema.MessageId, out var definition))
            {
                throw new InvalidOperationException($"Generated decoder schema message ID {schema.MessageId} is missing from the selected-dialect registry.");
            }

            if (schema.CrcExtra != definition.CrcExtra
                || schema.MinimumPayloadLength != definition.MinimumPayloadLength
                || schema.MaximumPayloadLength != definition.MaximumPayloadLength)
            {
                throw new InvalidOperationException(
                    $"Generated decoder schema for {definition.Name} ({definition.MessageId}) is incompatible with registry metadata. "
                    + $"Catalog CRC/lengths={schema.CrcExtra}/{schema.MinimumPayloadLength}-{schema.MaximumPayloadLength}; "
                    + $"registry={definition.CrcExtra}/{definition.MinimumPayloadLength}-{definition.MaximumPayloadLength}.");
            }
        }
    }

    private static void ValidateDeclaredSet(
        IEnumerable<IMavLinkMessageDecoder> decoders,
        IReadOnlyDictionary<uint, MavLinkDecoderCatalogSchema> schemas,
        MavLinkDecoderKind expectedKind,
        string setName)
    {
        var ids = ToUniqueDictionary(decoders, decoder => decoder.MessageId, setName).Keys.ToHashSet();
        var expected = schemas.Values.Where(schema => schema.Kind == expectedKind).Select(schema => schema.MessageId).ToHashSet();
        if (!ids.SetEquals(expected))
        {
            throw new InvalidOperationException($"The {setName} decoder set does not match its generated declarations.");
        }
    }

    private static void ValidateDeclaredCustomSet(
        IEnumerable<IMavLinkMessageDecoder> decoders,
        IReadOnlyDictionary<uint, MavLinkDecoderCatalogSchema> schemas)
    {
        var ids = ToUniqueDictionary(decoders, decoder => decoder.MessageId, "declared custom decoder").Keys.ToHashSet();
        var expected = schemas.Values
            .Where(schema => schema.Kind is MavLinkDecoderKind.HandWrittenOverride or MavLinkDecoderKind.ProtocolWorkflow)
            .Select(schema => schema.MessageId)
            .ToHashSet();
        if (!ids.SetEquals(expected))
        {
            throw new InvalidOperationException("A hand-written override or protocol decoder is missing or has not been declared in the generated catalog.");
        }
    }
}
