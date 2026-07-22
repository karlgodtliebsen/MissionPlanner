using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Compares official dialect definitions with the current implementation.
/// </summary>
public static partial class MavLinkCoverageAnalyzer
{
    /// <summary>
    /// Creates a coverage report for the repository.
    /// </summary>
    /// <param name="repositoryRoot">The repository root directory.</param>
    /// <param name="sourceRevision">The pinned dialect source revision.</param>
    /// <returns>The deterministic coverage report.</returns>
    public static MavLinkCoverageReport Create(string repositoryRoot, string sourceRevision)
    {
        var mavLinkRoot = Path.Combine(repositoryRoot, "src", "Core", "MissionPlanner.MavLink");
        var definitions = MavLinkDialectLoader.Load(Path.Combine(mavLinkRoot, "Dialects", "ardupilotmega.xml"));
        var constants = ReadMessageIds(Path.Combine(mavLinkRoot, "Messages", "MessageIds.cs"));
        var crcValues = ReadCrcProvider(
            Path.Combine(mavLinkRoot, "Services", "CommonMavLinkCrcExtraProvider.cs"),
            constants,
            definitions);
        var models = Directory.GetFiles(Path.Combine(mavLinkRoot, "Messages"), "*Message.cs").Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        var manualModelIds = Directory.GetFiles(Path.Combine(mavLinkRoot, "Messages"), "*Message.cs")
            .SelectMany(file => ManualModelIdRegex().Matches(File.ReadAllText(file)))
            .Select(match => match.Groups[1].Value)
            .Where(constants.ContainsKey)
            .Select(name => constants[name])
            .ToHashSet();
        var generatedModelIds = ReadGeneratedIds(
            Path.Combine(mavLinkRoot, "Generated", "MavLinkWireMessages.g.cs"),
            GeneratedModelIdRegex());
        var decoderResult = ReadDecoderIds(Path.Combine(mavLinkRoot, "Decoding"), constants, definitions);
        var coreRoot = Path.Combine(repositoryRoot, "src", "Core", "MissionPlanner.Core", "Vehicles");
        var handlerSources = Directory.GetFiles(Path.Combine(coreRoot, "Handlers"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        var entries = definitions.Select(definition =>
        {
            var className = ToPascalCase(definition.Name) + "Message";
            var matchingHandlers = handlerSources.Where(source => source.Contains(className, StringComparison.Ordinal)).ToArray();
            var hasHandler = matchingHandlers.Length > 0;
            var hasObservation = matchingHandlers.Any(source => ObservationConstructorRegex().IsMatch(source));
            var hasModel = models.Contains(className)
                || manualModelIds.Contains(definition.MessageId)
                || generatedModelIds.Contains(definition.MessageId);
            var hasDecoder = decoderResult.MessageIds.Contains(definition.MessageId);
            return new MavLinkCoverageEntry(
                definition.Dialect,
                definition.MessageId,
                definition.Name,
                definition.CrcExtra,
                definition.MinimumPayloadLength,
                definition.MaximumPayloadLength,
                constants.Values.Contains(definition.MessageId),
                crcValues.TryGetValue(definition.MessageId, out var crc) && crc == definition.CrcExtra,
                hasModel,
                hasDecoder,
                hasHandler,
                hasObservation,
                Classify(definition, hasModel, hasHandler));
        }).OrderBy(entry => entry.MessageId).ToArray();

        var byId = definitions.ToDictionary(definition => definition.MessageId);
        var incorrectConstants = constants.Where(item => item.Key != "DefaultFallback" && !byId.ContainsKey(item.Value))
            .Select(item => $"{item.Key}={item.Value}").Order(StringComparer.Ordinal).ToArray();
        var incorrectCrc = crcValues.Where(item => !byId.TryGetValue(item.Key, out var definition) || definition.CrcExtra != item.Value)
            .Select(item => $"{item.Key}={item.Value}").Order(StringComparer.Ordinal).ToArray();
        return new MavLinkCoverageReport(
            sourceRevision,
            "ardupilotmega.xml",
            entries,
            incorrectConstants,
            incorrectCrc,
            decoderResult.UnknownMessageIds);
    }

    /// <summary>
    /// Writes a stable machine-readable coverage report.
    /// </summary>
    /// <param name="report">The report to serialize.</param>
    /// <param name="outputPath">The destination JSON path.</param>
    public static void Write(MavLinkCoverageReport report, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, options) + Environment.NewLine);
    }

    private static Dictionary<string, uint> ReadMessageIds(string path) => ConstantRegex().Matches(File.ReadAllText(path))
        .ToDictionary(match => match.Groups[1].Value, match => uint.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), StringComparer.Ordinal);

    private static Dictionary<uint, byte> ReadCrcProvider(
        string path,
        IReadOnlyDictionary<string, uint> constants,
        IReadOnlyCollection<DialectMessageDefinition> definitions)
    {
        var source = File.ReadAllText(path);
        if (source.Contains("IMavLinkMessageDefinitionRegistry", StringComparison.Ordinal))
        {
            return definitions.ToDictionary(definition => definition.MessageId, definition => definition.CrcExtra);
        }

        var result = new Dictionary<uint, byte>();
        foreach (Match match in CrcCaseRegex().Matches(source))
        {
            if (constants.TryGetValue(match.Groups[1].Value, out var id))
            {
                result[id] = byte.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            }
        }
        return result;
    }

    private static DecoderScanResult ReadDecoderIds(
        string path,
        IReadOnlyDictionary<string, uint> constants,
        IReadOnlyCollection<DialectMessageDefinition> definitions)
    {
        var messageIds = new HashSet<uint>();
        var unknownMessageIds = new List<string>();
        var officialIds = definitions.Select(definition => definition.MessageId).ToHashSet();
        foreach (var file in Directory.GetFiles(path, "*MessageDecoder.cs").Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).Equals("RawMavLinkMessageDecoder.cs", StringComparison.Ordinal)
                || Path.GetFileName(file).Equals("GeneratedMavLinkMessageDecoder.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var match = DecoderIdRegex().Match(File.ReadAllText(file));
            if (!match.Success)
            {
                unknownMessageIds.Add($"{Path.GetFileName(file)}: no MessageIds constant");
                continue;
            }

            var constantName = match.Groups[1].Value;
            if (!constants.TryGetValue(constantName, out var id))
            {
                unknownMessageIds.Add($"{Path.GetFileName(file)}: MessageIds.{constantName} is undefined");
                continue;
            }

            messageIds.Add(id);
            if (!officialIds.Contains(id))
            {
                unknownMessageIds.Add($"{Path.GetFileName(file)}: MessageIds.{constantName}={id}");
            }
        }

        var generatedPath = Path.Combine(Path.GetDirectoryName(path)!, "Generated", "MavLinkWireDecoders.g.cs");
        foreach (var id in ReadGeneratedIds(generatedPath, GeneratedDecoderIdRegex()))
        {
            messageIds.Add(id);
            if (!officialIds.Contains(id))
            {
                unknownMessageIds.Add($"MavLinkWireDecoders.g.cs: message ID {id}");
            }
        }

        return new DecoderScanResult(messageIds, unknownMessageIds);
    }

    private static HashSet<uint> ReadGeneratedIds(string path, Regex regex)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return regex.Matches(File.ReadAllText(path))
            .Select(match => uint.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    private static MavLinkCoverageClassification Classify(DialectMessageDefinition definition, bool hasModel, bool hasHandler)
    {
        if (definition.IsDeprecated) return MavLinkCoverageClassification.Deprecated;
        if (hasHandler) return MavLinkCoverageClassification.DomainTelemetry;
        if (definition.Name.StartsWith("MISSION_", StringComparison.Ordinal) || definition.Name.StartsWith("PARAM_", StringComparison.Ordinal) || definition.Name is "COMMAND_ACK" or "FILE_TRANSFER_PROTOCOL") return MavLinkCoverageClassification.ProtocolWorkflow;
        return hasModel ? MavLinkCoverageClassification.TypedWireMessage : MavLinkCoverageClassification.RegistryOnly;
    }

    private static string ToPascalCase(string value) => string.Concat(value.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    [GeneratedRegex(@"public\s+const\s+uint\s+(\w+)\s*=\s*(\d+)")]
    private static partial Regex ConstantRegex();

    [GeneratedRegex(@"case\s+MessageIds\.(\w+):\s*crcExtra\s*=\s*(\d+)", RegexOptions.Singleline)]
    private static partial Regex CrcCaseRegex();

    [GeneratedRegex(@"MessageIds\.(\w+)")]
    private static partial Regex DecoderIdRegex();

    [GeneratedRegex(@"new\s+Vehicle\w+Observation\s*\(")]
    private static partial Regex ObservationConstructorRegex();

    [GeneratedRegex(@":\s*GeneratedMavLinkMessage\(SystemId,\s*ComponentId,\s*(\d+)u,")]
    private static partial Regex GeneratedModelIdRegex();

    [GeneratedRegex(@":\s*base\(definitions,\s*(\d+)u\)")]
    private static partial Regex GeneratedDecoderIdRegex();

    [GeneratedRegex(@":\s*MavLinkMessage\(SystemId,\s*ComponentId,\s*MessageIds\.(\w+)")]
    private static partial Regex ManualModelIdRegex();

    private sealed record DecoderScanResult(HashSet<uint> MessageIds, IReadOnlyList<string> UnknownMessageIds);
}
