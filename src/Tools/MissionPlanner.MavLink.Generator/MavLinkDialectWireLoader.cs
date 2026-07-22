using System.Globalization;
using System.Xml.Linq;

namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Resolves complete wire-message schemas from a MAVLink dialect include graph.
/// </summary>
public static class MavLinkDialectWireLoader
{
    /// <summary>
    /// Loads the root dialect and all transitive message definitions.
    /// </summary>
    /// <param name="rootDialectPath">The root dialect XML path.</param>
    /// <returns>The resolved schemas ordered by numeric message ID.</returns>
    public static IReadOnlyList<DialectWireMessageDefinition> Load(string rootDialectPath)
    {
        var registryDefinitions = MavLinkDialectLoader.Load(rootDialectPath)
            .ToDictionary(definition => definition.MessageId);
        var messages = new Dictionary<uint, DialectWireMessageDefinition>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadFile(Path.GetFullPath(rootDialectPath), registryDefinitions, messages, visited);
        return messages.Values.OrderBy(message => message.MessageId).ToArray();
    }

    private static void LoadFile(
        string path,
        IReadOnlyDictionary<uint, DialectMessageDefinition> registryDefinitions,
        IDictionary<uint, DialectWireMessageDefinition> messages,
        ISet<string> visited)
    {
        if (!visited.Add(path))
        {
            return;
        }

        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException($"Dialect '{path}' has no root element.");
        foreach (var include in root.Elements("include"))
        {
            LoadFile(Path.Combine(Path.GetDirectoryName(path)!, include.Value.Trim()), registryDefinitions, messages, visited);
        }

        var dialect = Path.GetFileNameWithoutExtension(path);
        foreach (var element in root.Element("messages")?.Elements("message") ?? [])
        {
            var id = uint.Parse(element.Attribute("id")?.Value ?? throw new InvalidDataException("Message ID is missing."), CultureInfo.InvariantCulture);
            var registry = registryDefinitions[id];
            var fields = ParseFields(element);
            var definition = new DialectWireMessageDefinition(
                id,
                registry.Name,
                Normalize(element.Element("description")?.Value),
                registry.CrcExtra,
                registry.MinimumPayloadLength,
                registry.MaximumPayloadLength,
                dialect,
                registry.IsDeprecated,
                fields);
            if (messages.TryGetValue(id, out var existing) && existing != definition)
            {
                throw new InvalidDataException($"Message ID {id} has conflicting wire schemas.");
            }

            messages[id] = definition;
        }
    }

    private static IReadOnlyList<DialectWireFieldDefinition> ParseFields(XElement message)
    {
        var fields = new List<MutableField>();
        var extension = false;
        foreach (var element in message.Elements())
        {
            if (element.Name.LocalName == "extensions")
            {
                extension = true;
                continue;
            }

            if (element.Name.LocalName != "field")
            {
                continue;
            }

            var declared = element.Attribute("type")?.Value ?? throw new InvalidDataException("Field type is missing.");
            var bracket = declared.IndexOf('[', StringComparison.Ordinal);
            var scalarType = bracket < 0 ? declared : declared[..bracket];
            var arrayLength = bracket < 0
                ? 1
                : int.Parse(declared[(bracket + 1)..declared.IndexOf(']', bracket)], CultureInfo.InvariantCulture);
            fields.Add(new MutableField(
                element.Attribute("name")?.Value ?? throw new InvalidDataException("Field name is missing."),
                scalarType,
                arrayLength,
                extension,
                fields.Count,
                GetTypeSize(scalarType),
                Normalize(element.Value),
                element.Attribute("enum")?.Value));
        }

        var offset = 0;
        foreach (var field in fields.Where(field => !field.IsExtension)
                     .OrderByDescending(field => field.ElementSize)
                     .ThenBy(field => field.SourceIndex)
                     .Concat(fields.Where(field => field.IsExtension).OrderBy(field => field.SourceIndex)))
        {
            field.WireOffset = offset;
            offset += field.ElementSize * field.ArrayLength;
        }

        return fields.Select(field => new DialectWireFieldDefinition(
            field.Name,
            field.DeclaredType,
            field.ArrayLength,
            field.IsExtension,
            field.SourceIndex,
            field.WireOffset,
            field.ElementSize,
            field.Description,
            field.EnumName)).ToArray();
    }

    private static int GetTypeSize(string type) => type switch
    {
        "char" or "int8_t" or "uint8_t" or "uint8_t_mavlink_version" => 1,
        "int16_t" or "uint16_t" => 2,
        "int32_t" or "uint32_t" or "float" => 4,
        "int64_t" or "uint64_t" or "double" => 8,
        _ => throw new InvalidDataException($"Unsupported MAVLink field type '{type}'.")
    };

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed class MutableField(
        string name,
        string declaredType,
        int arrayLength,
        bool isExtension,
        int sourceIndex,
        int elementSize,
        string description,
        string? enumName)
    {
        public string Name { get; } = name;
        public string DeclaredType { get; } = declaredType;
        public int ArrayLength { get; } = arrayLength;
        public bool IsExtension { get; } = isExtension;
        public int SourceIndex { get; } = sourceIndex;
        public int ElementSize { get; } = elementSize;
        public string Description { get; } = description;
        public string? EnumName { get; } = enumName;
        public int WireOffset { get; set; }
    }
}
