using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Resolves inherited MAVLink XML dialects and computes their wire definitions.
/// </summary>
public static class MavLinkDialectLoader
{
    /// <summary>
    /// Loads a root dialect and all of its transitive includes.
    /// </summary>
    /// <param name="rootDialectPath">The root dialect XML file.</param>
    /// <returns>Definitions ordered by message ID.</returns>
    public static IReadOnlyList<DialectMessageDefinition> Load(string rootDialectPath)
    {
        var definitions = new Dictionary<uint, DialectMessageDefinition>();
        var names = new Dictionary<string, uint>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadFile(Path.GetFullPath(rootDialectPath), definitions, names, visited);
        return definitions.Values.OrderBy(definition => definition.MessageId).ToArray();
    }

    private static void LoadFile(
        string path,
        IDictionary<uint, DialectMessageDefinition> definitions,
        IDictionary<string, uint> names,
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
            var includeName = include.Value.Trim();
            LoadFile(Path.Combine(Path.GetDirectoryName(path)!, includeName), definitions, names, visited);
        }

        var dialect = Path.GetFileNameWithoutExtension(path);
        foreach (var message in root.Element("messages")?.Elements("message") ?? [])
        {
            var definition = CreateDefinition(message, dialect);
            if (definitions.TryGetValue(definition.MessageId, out var existing) && existing != definition)
            {
                throw new InvalidDataException($"Message ID {definition.MessageId} is defined by both {existing.Name} and {definition.Name}.");
            }

            if (names.TryGetValue(definition.Name, out var existingId) && existingId != definition.MessageId)
            {
                throw new InvalidDataException($"Message name {definition.Name} has conflicting IDs {existingId} and {definition.MessageId}.");
            }

            definitions[definition.MessageId] = definition;
            names[definition.Name] = definition.MessageId;
        }
    }

    private static DialectMessageDefinition CreateDefinition(XElement message, string dialect)
    {
        var id = uint.Parse(message.Attribute("id")?.Value ?? throw new InvalidDataException("Message ID is missing."), CultureInfo.InvariantCulture);
        var name = message.Attribute("name")?.Value ?? throw new InvalidDataException($"Message {id} has no name.");
        var fields = new List<WireField>();
        var extensions = false;
        var index = 0;
        foreach (var element in message.Elements())
        {
            if (element.Name.LocalName == "extensions")
            {
                extensions = true;
                continue;
            }

            if (element.Name.LocalName != "field")
            {
                continue;
            }

            var declaredType = element.Attribute("type")?.Value ?? throw new InvalidDataException($"Field type is missing in {name}.");
            var fieldName = element.Attribute("name")?.Value ?? throw new InvalidDataException($"Field name is missing in {name}.");
            fields.Add(ParseField(declaredType, fieldName, extensions, index++));
        }

        var baseFields = fields.Where(field => !field.IsExtension)
            .OrderByDescending(field => field.ElementSize)
            .ThenBy(field => field.SourceIndex)
            .ToArray();
        var extensionFields = fields.Where(field => field.IsExtension).ToArray();
        var minimumLength = checked((byte)baseFields.Sum(field => field.WireLength));
        var maximumLength = checked((byte)(minimumLength + extensionFields.Sum(field => field.WireLength)));
        var crcExtra = CalculateCrcExtra(name, baseFields);
        var deprecated = message.Element("deprecated") is not null || message.Attribute("deprecated") is not null;
        return new DialectMessageDefinition(id, name, crcExtra, minimumLength, maximumLength, dialect, deprecated);
    }

    private static WireField ParseField(string declaredType, string name, bool extension, int index)
    {
        var bracket = declaredType.IndexOf('[', StringComparison.Ordinal);
        var type = bracket < 0 ? declaredType : declaredType[..bracket];
        var arrayLength = bracket < 0
            ? 1
            : int.Parse(declaredType[(bracket + 1)..declaredType.IndexOf(']', bracket)], CultureInfo.InvariantCulture);
        var crcType = type == "uint8_t_mavlink_version" ? "uint8_t" : type;
        var size = GetTypeSize(type);
        return new WireField(crcType, name, size, checked(size * arrayLength), arrayLength, extension, index);
    }

    private static int GetTypeSize(string type) => type switch
    {
        "char" or "int8_t" or "uint8_t" or "uint8_t_mavlink_version" => 1,
        "int16_t" or "uint16_t" => 2,
        "int32_t" or "uint32_t" or "float" => 4,
        "int64_t" or "uint64_t" or "double" => 8,
        _ => throw new InvalidDataException($"Unsupported MAVLink field type '{type}'.")
    };

    private static byte CalculateCrcExtra(string messageName, IEnumerable<WireField> fields)
    {
        ushort crc = 0xffff;
        Accumulate(Encoding.ASCII.GetBytes(messageName + " "), ref crc);
        foreach (var field in fields)
        {
            Accumulate(Encoding.ASCII.GetBytes(field.CrcType + " "), ref crc);
            Accumulate(Encoding.ASCII.GetBytes(field.Name + " "), ref crc);
            if (field.ArrayLength > 1)
            {
                Accumulate((byte)field.ArrayLength, ref crc);
            }
        }

        return (byte)((crc & 0xff) ^ (crc >> 8));
    }

    private static void Accumulate(IEnumerable<byte> bytes, ref ushort crc)
    {
        foreach (var value in bytes)
        {
            Accumulate(value, ref crc);
        }
    }

    private static void Accumulate(byte value, ref ushort crc)
    {
        var temporary = (byte)(value ^ (byte)crc);
        temporary ^= (byte)(temporary << 4);
        crc = (ushort)((crc >> 8) ^ (temporary << 8) ^ (temporary << 3) ^ (temporary >> 4));
    }

    private sealed record WireField(string CrcType, string Name, int ElementSize, int WireLength, int ArrayLength, bool IsExtension, int SourceIndex);
}
