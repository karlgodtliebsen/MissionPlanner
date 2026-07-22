using System.Globalization;
using System.Net;
using System.Text;

namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Emits strongly typed MAVLink wire models and their decoders.
/// </summary>
public static class MavLinkWireModelSourceGenerator
{
    private static readonly HashSet<string> Overrides = new(StringComparer.Ordinal)
    {
        "AHRS2",
        "ATTITUDE",
        "AUTOPILOT_VERSION",
        "BATTERY_STATUS",
        "COMMAND_ACK",
        "EKF_STATUS_REPORT",
        "FILE_TRANSFER_PROTOCOL",
        "GLOBAL_POSITION_INT",
        "GPS_RAW_INT",
        "HEARTBEAT",
        "HOME_POSITION",
        "LOCAL_POSITION_NED",
        "MEMINFO",
        "MEMORY_VECT",
        "MISSION_ACK",
        "MISSION_COUNT",
        "MISSION_CURRENT",
        "MISSION_ITEM_INT",
        "MISSION_ITEM_REACHED",
        "MISSION_REQUEST_INT",
        "MISSION_REQUEST_LIST",
        "NAV_CONTROLLER_OUTPUT",
        "PARAM_VALUE",
        "POWER_STATUS",
        "RAW_IMU",
        "RC_CHANNELS",
        "SCALED_PRESSURE",
        "SERVO_OUTPUT_RAW",
        "STATUSTEXT",
        "SYS_STATUS",
        "TIMESYNC",
        "VFR_HUD"
    };

    private static readonly HashSet<string> DeprecatedGeneratedExceptions = new(StringComparer.Ordinal)
    {
        "BATTERY2",
        "HWSTATUS",
        "MISSION_ITEM",
        "MISSION_REQUEST"
    };

    private static readonly HashSet<string> ProtocolWorkflowOverrides = new(StringComparer.Ordinal)
    {
        "COMMAND_ACK",
        "FILE_TRANSFER_PROTOCOL",
        "MISSION_ACK",
        "MISSION_COUNT",
        "MISSION_CURRENT",
        "MISSION_ITEM_INT",
        "MISSION_ITEM_REACHED",
        "MISSION_REQUEST_INT",
        "MISSION_REQUEST_LIST",
        "PARAM_VALUE",
        "TIMESYNC"
    };

    /// <summary>
    /// Gets the message names whose established hand-written models and decoders are retained.
    /// </summary>
    public static IReadOnlySet<string> HandWrittenOverrides => Overrides;

    /// <summary>
    /// Generates the wire model source.
    /// </summary>
    /// <param name="definitions">The resolved wire schemas.</param>
    /// <param name="sourceRevision">The pinned MAVLink revision.</param>
    /// <returns>The generated C# source.</returns>
    public static string GenerateModels(IReadOnlyCollection<DialectWireMessageDefinition> definitions, string sourceRevision)
    {
        var messages = SelectGenerated(definitions);
        var source = CreateHeader(sourceRevision, "typed wire models");
        source.AppendLine("using MissionPlanner.MavLink.Decoding.Utils;");
        source.AppendLine("using MissionPlanner.Transport;");
        source.AppendLine();
        source.AppendLine("namespace MissionPlanner.MavLink.Messages;");
        foreach (var message in messages)
        {
            AppendModel(source, message);
        }

        return source.ToString();
    }

    /// <summary>
    /// Generates the decoder and decoder-catalog source.
    /// </summary>
    /// <param name="definitions">The resolved wire schemas.</param>
    /// <param name="sourceRevision">The pinned MAVLink revision.</param>
    /// <returns>The generated C# source.</returns>
    public static string GenerateDecoders(IReadOnlyCollection<DialectWireMessageDefinition> definitions, string sourceRevision)
    {
        var messages = SelectGenerated(definitions);
        var source = CreateHeader(sourceRevision, "typed wire decoders");
        source.AppendLine("using MissionPlanner.MavLink.Decoding.Utils;");
        source.AppendLine("using MissionPlanner.MavLink.Messages;");
        source.AppendLine("using MissionPlanner.MavLink.Services.Abstractions;");
        source.AppendLine();
        source.AppendLine("namespace MissionPlanner.MavLink.Decoding;");
        foreach (var message in messages)
        {
            AppendDecoder(source, message);
        }

        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Creates all generated selected-dialect message decoders.");
        source.AppendLine("/// </summary>");
        source.AppendLine("public static class GeneratedMavLinkMessageDecoderCatalog");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Creates the generated decoder set using the central message-definition registry.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    /// <param name=\"definitions\">The selected-dialect definition registry.</param>");
        source.AppendLine("    /// <returns>The generated decoders, ordered by message ID.</returns>");
        source.AppendLine("    public static IReadOnlyList<IMavLinkMessageDecoder> Create(IMavLinkMessageDefinitionRegistry definitions) =>");
        source.AppendLine("    [");
        foreach (var message in messages)
        {
            source.Append("        new ").Append(GetTypeName(message)).AppendLine("Decoder(definitions),");
        }

        source.AppendLine("    ];");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Creates all declared hand-written override and protocol-workflow decoders.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    /// <returns>The declared custom decoder set, ordered by message ID.</returns>");
        source.AppendLine("    public static IReadOnlyList<IMavLinkMessageDecoder> CreateDeclaredCustomDecoders() =>");
        source.AppendLine("    [");
        foreach (var message in SelectTyped(definitions).Where(message => Overrides.Contains(message.Name)))
        {
            source.Append("        new ").Append(GetCustomDecoderTypeName(message.Name)).AppendLine("(),");
        }

        source.AppendLine("    ];");
        source.AppendLine();
        source.AppendLine("    /// <summary>");
        source.AppendLine("    /// Gets independent generated CRC, payload-length, and ownership expectations for typed messages.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    public static IReadOnlyList<MavLinkDecoderCatalogSchema> Schemas { get; } =");
        source.AppendLine("    [");
        foreach (var message in SelectTyped(definitions))
        {
            var kind = !Overrides.Contains(message.Name)
                ? "Generated"
                : ProtocolWorkflowOverrides.Contains(message.Name) ? "ProtocolWorkflow" : "HandWrittenOverride";
            source.Append("        new(")
                .Append(message.MessageId.ToString(CultureInfo.InvariantCulture)).Append("u, ")
                .Append(message.CrcExtra.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(message.MinimumPayloadLength.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(message.MaximumPayloadLength.ToString(CultureInfo.InvariantCulture)).Append(", MavLinkDecoderKind.")
                .Append(kind).AppendLine("),");
        }

        source.AppendLine("    ];");
        source.AppendLine("}");
        return source.ToString();
    }

    private static IReadOnlyList<DialectWireMessageDefinition> SelectGenerated(IEnumerable<DialectWireMessageDefinition> definitions) =>
        SelectTyped(definitions).Where(message => !Overrides.Contains(message.Name))
            .OrderBy(message => message.MessageId)
            .ToArray();

    private static IReadOnlyList<DialectWireMessageDefinition> SelectTyped(IEnumerable<DialectWireMessageDefinition> definitions) =>
        definitions.Where(message => !message.IsDeprecated || DeprecatedGeneratedExceptions.Contains(message.Name))
            .OrderBy(message => message.MessageId)
            .ToArray();

    private static string GetCustomDecoderTypeName(string messageName) => messageName switch
    {
        "MEMINFO" => "MemInfoMessageDecoder",
        "STATUSTEXT" => "StatusTextMessageDecoder",
        "TIMESYNC" => "TimeSyncMessageDecoder",
        _ => MavLinkEnumSourceGenerator.ToIdentifier(messageName) + "MessageDecoder"
    };

    private static StringBuilder CreateHeader(string revision, string content)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.Append("// Generated ").Append(content).AppendLine(" from ardupilotmega.xml and its transitive includes.");
        source.Append("// MAVLink source revision: ").AppendLine(revision);
        source.AppendLine("// Hand-written overrides are declared by MavLinkWireModelSourceGenerator.HandWrittenOverrides.");
        source.AppendLine("// DO NOT EDIT MANUALLY. Regenerate with MissionPlanner.MavLink.Generator.");
        source.AppendLine();
        return source;
    }

    private static void AppendModel(StringBuilder source, DialectWireMessageDefinition message)
    {
        source.AppendLine();
        AppendSummary(source, message.Description, $"Represents the MAVLink {message.Name} wire message.");
        AppendParameter(source, "SystemId", "The source MAVLink system ID.");
        AppendParameter(source, "ComponentId", "The source MAVLink component ID.");
        AppendParameter(source, "EndPoint", "The source transport endpoint.");
        foreach (var field in message.Fields)
        {
            AppendParameter(source, GetPropertyName(field), field.Description.Length == 0 ? $"The {field.Name} field." : field.Description);
        }

        AppendParameter(source, "ReceivedAt", "The reception timestamp.");
        source.Append("public sealed record ").Append(GetTypeName(message)).AppendLine("(");
        source.AppendLine("    byte SystemId,");
        source.AppendLine("    byte ComponentId,");
        source.AppendLine("    TransportEndPoint EndPoint,");
        foreach (var field in message.Fields)
        {
            source.Append("    ").Append(GetPropertyType(field)).Append(' ').Append(GetPropertyName(field)).AppendLine(",");
        }

        source.AppendLine("    DateTimeOffset ReceivedAt)");
        source.Append("    : GeneratedMavLinkMessage(SystemId, ComponentId, ")
            .Append(message.MessageId.ToString(CultureInfo.InvariantCulture)).AppendLine("u, EndPoint, ReceivedAt)");
        source.AppendLine("{");
        source.Append("    internal override int MinimumPayloadLength => ").Append(message.MinimumPayloadLength.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        source.Append("    internal override int MaximumPayloadLength => ").Append(message.MaximumPayloadLength.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        source.AppendLine();
        source.AppendLine("    internal override void WritePayload(Span<byte> payload)");
        source.AppendLine("    {");
        foreach (var field in message.Fields.OrderBy(field => field.WireOffset))
        {
            source.Append("        ").Append(GetWriteExpression(field)).AppendLine(";");
        }

        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static void AppendDecoder(StringBuilder source, DialectWireMessageDefinition message)
    {
        var typeName = GetTypeName(message);
        source.AppendLine();
        AppendSummary(source, string.Empty, $"Decodes MAVLink {message.Name} wire messages.");
        source.Append("internal sealed class ").Append(typeName).AppendLine("Decoder : GeneratedMavLinkMessageDecoder");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>");
        source.Append("    /// Initializes a decoder for MAVLink ").Append(message.Name).AppendLine(" messages.");
        source.AppendLine("    /// </summary>");
        source.AppendLine("    /// <param name=\"definitions\">The central message-definition registry.</param>");
        source.Append("    internal ").Append(typeName).Append("Decoder(IMavLinkMessageDefinitionRegistry definitions) : base(definitions, ")
            .Append(message.MessageId.ToString(CultureInfo.InvariantCulture)).AppendLine("u)");
        source.AppendLine("    {");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    protected override MavLinkMessage DecodeCore(MavLinkFrame frame, ReadOnlySpan<byte> payload) =>");
        source.Append("        new ").Append(typeName).AppendLine("(");
        source.AppendLine("            frame.SystemId,");
        source.AppendLine("            frame.ComponentId,");
        source.AppendLine("            frame.EndPoint,");
        foreach (var field in message.Fields)
        {
            source.Append("            ").Append(GetReadExpression(field)).AppendLine(",");
        }

        source.AppendLine("            frame.ReceivedAt);");
        source.AppendLine("}");
    }

    private static string GetTypeName(DialectWireMessageDefinition message) =>
        MavLinkEnumSourceGenerator.ToIdentifier(message.Name) + "Message";

    private static string GetPropertyName(DialectWireFieldDefinition field)
    {
        var name = MavLinkEnumSourceGenerator.ToIdentifier(field.Name);
        return name == "MessageId" ? "PayloadMessageId" : name;
    }

    private static string GetPropertyType(DialectWireFieldDefinition field)
    {
        if (field.DeclaredType == "char" && field.ArrayLength > 1)
        {
            return "string";
        }

        var scalar = GetScalarType(field.DeclaredType);
        return field.ArrayLength > 1 ? scalar + "[]" : scalar;
    }

    private static string GetScalarType(string type) => type switch
    {
        "char" or "uint8_t" or "uint8_t_mavlink_version" => "byte",
        "int8_t" => "sbyte",
        "uint16_t" => "ushort",
        "int16_t" => "short",
        "uint32_t" => "uint",
        "int32_t" => "int",
        "uint64_t" => "ulong",
        "int64_t" => "long",
        "float" => "float",
        "double" => "double",
        _ => throw new InvalidDataException($"Unsupported MAVLink type '{type}'.")
    };

    private static string GetCodecSuffix(string type) => type switch
    {
        "char" or "uint8_t" or "uint8_t_mavlink_version" => "Byte",
        "int8_t" => "SByte",
        "uint16_t" => "UInt16",
        "int16_t" => "Int16",
        "uint32_t" => "UInt32",
        "int32_t" => "Int32",
        "uint64_t" => "UInt64",
        "int64_t" => "Int64",
        "float" => "Single",
        "double" => "Double",
        _ => throw new InvalidDataException($"Unsupported MAVLink type '{type}'.")
    };

    private static string GetReadExpression(DialectWireFieldDefinition field)
    {
        var offset = field.WireOffset.ToString(CultureInfo.InvariantCulture);
        if (field.DeclaredType == "char" && field.ArrayLength > 1)
        {
            return $"MavLinkWireCodec.ReadString(payload, {offset}, {field.ArrayLength.ToString(CultureInfo.InvariantCulture)})";
        }

        var suffix = GetCodecSuffix(field.DeclaredType);
        if (field.ArrayLength > 1)
        {
            return $"MavLinkWireCodec.ReadArray(payload, {offset}, {field.ArrayLength.ToString(CultureInfo.InvariantCulture)}, {field.ElementSize.ToString(CultureInfo.InvariantCulture)}, MavLinkWireCodec.Read{suffix})";
        }

        return $"MavLinkWireCodec.Read{suffix}(payload, {offset})";
    }

    private static string GetWriteExpression(DialectWireFieldDefinition field)
    {
        var property = GetPropertyName(field);
        var offset = field.WireOffset.ToString(CultureInfo.InvariantCulture);
        if (field.DeclaredType == "char" && field.ArrayLength > 1)
        {
            return $"MavLinkWireCodec.WriteString(payload, {offset}, {field.ArrayLength.ToString(CultureInfo.InvariantCulture)}, {property})";
        }

        var suffix = GetCodecSuffix(field.DeclaredType);
        if (field.ArrayLength > 1)
        {
            return $"MavLinkWireCodec.WriteArray(payload, {offset}, {field.ArrayLength.ToString(CultureInfo.InvariantCulture)}, {field.ElementSize.ToString(CultureInfo.InvariantCulture)}, {property}, MavLinkWireCodec.Write{suffix})";
        }

        return $"MavLinkWireCodec.Write{suffix}(payload, {offset}, {property})";
    }

    private static void AppendSummary(StringBuilder source, string description, string fallback)
    {
        source.AppendLine("/// <summary>");
        source.Append("/// ").AppendLine(WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(description) ? fallback : description));
        source.AppendLine("/// </summary>");
    }

    private static void AppendParameter(StringBuilder source, string name, string description) =>
        source.Append("/// <param name=\"").Append(name).Append("\">")
            .Append(WebUtility.HtmlEncode(description)).AppendLine("</param>");
}
