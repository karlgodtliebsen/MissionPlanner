using System.Globalization;
using System.Xml.Linq;

namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Resolves and merges MAVLink enums across a dialect include graph.
/// </summary>
public static class MavLinkDialectEnumLoader
{
    /// <summary>
    /// Loads every enum declared by a root dialect and its transitive includes.
    /// </summary>
    /// <param name="rootDialectPath">The root dialect XML file.</param>
    /// <returns>Resolved enum definitions ordered by XML name.</returns>
    public static IReadOnlyList<DialectEnumDefinition> Load(string rootDialectPath)
    {
        var builders = new Dictionary<string, EnumBuilder>(StringComparer.Ordinal);
        var referencedWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadFile(Path.GetFullPath(rootDialectPath), builders, referencedWidths, visited);

        return builders.Values
            .Select(builder => builder.Build(referencedWidths.GetValueOrDefault(builder.Name)))
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void LoadFile(
        string path,
        IDictionary<string, EnumBuilder> builders,
        IDictionary<string, int> referencedWidths,
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
            LoadFile(Path.Combine(Path.GetDirectoryName(path)!, include.Value.Trim()), builders, referencedWidths, visited);
        }

        var dialect = Path.GetFileNameWithoutExtension(path);
        foreach (var enumElement in root.Element("enums")?.Elements("enum") ?? [])
        {
            var name = RequiredAttribute(enumElement, "name");
            if (!builders.TryGetValue(name, out var builder))
            {
                builder = new EnumBuilder(name, dialect);
                builders.Add(name, builder);
            }

            builder.Merge(enumElement);
        }

        foreach (var field in root.Descendants("field").Where(element => element.Attribute("enum") is not null))
        {
            RecordWidth(field, referencedWidths);
        }
    }

    private static void RecordWidth(XElement field, IDictionary<string, int> referencedWidths)
    {
        var enumName = RequiredAttribute(field, "enum");
        var declaredType = RequiredAttribute(field, "type");
        var bracket = declaredType.IndexOf('[', StringComparison.Ordinal);
        var type = bracket < 0 ? declaredType : declaredType[..bracket];
        var width = type switch
        {
            "char" or "int8_t" or "uint8_t" or "uint8_t_mavlink_version" => 1,
            "int16_t" or "uint16_t" => 2,
            "int32_t" or "uint32_t" or "float" => 4,
            "int64_t" or "uint64_t" or "double" => 8,
            _ => 0
        };

        referencedWidths.TryGetValue(enumName, out var existingWidth);
        referencedWidths[enumName] = Math.Max(existingWidth, width);
    }

    private static string RequiredAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value
        ?? throw new InvalidDataException($"Element '{element.Name}' is missing attribute '{name}'.");

    private sealed class EnumBuilder(string name, string dialect)
    {
        private readonly Dictionary<string, DialectEnumEntryDefinition> entries = new(StringComparer.Ordinal);
        private string description = string.Empty;
        private bool isBitmask;

        public string Name { get; } = name;

        public void Merge(XElement element)
        {
            isBitmask |= string.Equals(element.Attribute("bitmask")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            description = string.IsNullOrWhiteSpace(description)
                ? Normalize(element.Element("description")?.Value)
                : description;

            foreach (var entry in element.Elements("entry"))
            {
                var entryName = RequiredAttribute(entry, "name");
                var value = ulong.Parse(RequiredAttribute(entry, "value"), CultureInfo.InvariantCulture);
                var definition = new DialectEnumEntryDefinition(entryName, value, Normalize(entry.Element("description")?.Value));
                if (entries.TryGetValue(entryName, out var existing) && existing.Value != value)
                {
                    throw new InvalidDataException($"Enum entry {entryName} has conflicting values {existing.Value} and {value}.");
                }

                entries[entryName] = definition;
            }
        }

        public DialectEnumDefinition Build(int referencedWidth)
        {
            var orderedEntries = entries.Values.OrderBy(entry => entry.Value).ThenBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
            var maximum = orderedEntries.Length == 0 ? 0UL : orderedEntries.Max(entry => entry.Value);
            var requiredWidth = maximum switch
            {
                <= byte.MaxValue => 1,
                <= ushort.MaxValue => 2,
                <= uint.MaxValue => 4,
                _ => 8
            };
            var width = Math.Max(requiredWidth, referencedWidth);
            var underlyingType = width switch
            {
                <= 1 => "byte",
                2 => "ushort",
                4 => "uint",
                _ => "ulong"
            };
            return new DialectEnumDefinition(Name, dialect, description, isBitmask, underlyingType, orderedEntries);
        }
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
