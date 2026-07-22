using MissionPlanner.MavLink.Generator;
using System.Text.Json;

if (args is ["registry", var repositoryRoot, var registryOutput])
{
    var manifest = LoadManifest(repositoryRoot);
    var definitions = MavLinkDialectLoader.Load(Path.Combine(
        repositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        manifest.RootDialect));
    var source = MavLinkRegistrySourceGenerator.Generate(definitions, manifest.SourceRevision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(registryOutput))!);
    File.WriteAllText(registryOutput, source);
    Console.WriteLine($"Wrote {definitions.Count} message definitions to {registryOutput}.");
    return 0;
}

if (args is ["enums", var enumRepositoryRoot, var enumOutput])
{
    var manifest = LoadManifest(enumRepositoryRoot);
    var definitions = MavLinkDialectEnumLoader.Load(Path.Combine(
        enumRepositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        manifest.RootDialect));
    var source = MavLinkEnumSourceGenerator.Generate(definitions, manifest.SourceRevision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(enumOutput))!);
    File.WriteAllText(enumOutput, source);
    Console.WriteLine($"Wrote {definitions.Count} protocol enums to {enumOutput}.");
    return 0;
}

if (args is ["wire", var wireRepositoryRoot, var modelOutput, var decoderOutput])
{
    var manifest = LoadManifest(wireRepositoryRoot);
    var definitions = MavLinkDialectWireLoader.Load(Path.Combine(
        wireRepositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        manifest.RootDialect));
    var modelSource = MavLinkWireModelSourceGenerator.GenerateModels(definitions, manifest.SourceRevision);
    var decoderSource = MavLinkWireModelSourceGenerator.GenerateDecoders(definitions, manifest.SourceRevision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(modelOutput))!);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(decoderOutput))!);
    File.WriteAllText(modelOutput, modelSource);
    File.WriteAllText(decoderOutput, decoderSource);
    var generatedCount = definitions.Count(definition =>
        (!definition.IsDeprecated || MavLinkWireModelSourceGenerator.DeprecatedCompatibilityMessages.Contains(definition.Name))
        && !MavLinkWireModelSourceGenerator.HandWrittenOverrides.Contains(definition.Name));
    Console.WriteLine($"Wrote {generatedCount} wire models and decoders to {modelOutput} and {decoderOutput}.");
    return 0;
}

if (args is ["promotion", var promotionRepositoryRoot, var promotionOutput])
{
    var manifest = LoadManifest(promotionRepositoryRoot);
    var definitions = MavLinkDialectLoader.Load(Path.Combine(
        promotionRepositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        manifest.RootDialect));
    var catalog = MavLinkPromotionCatalogGenerator.Generate(definitions, manifest.SourceRevision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(promotionOutput))!);
    File.WriteAllText(promotionOutput, catalog);
    Console.WriteLine($"Wrote {definitions.Count} promotion entries to {promotionOutput}.");
    return 0;
}

if (args.Length is < 1 or > 4)
{
    Console.Error.WriteLine("Usage: MissionPlanner.MavLink.Generator <repository-root> [output-json]");
    Console.Error.WriteLine("   or: MissionPlanner.MavLink.Generator registry <repository-root> <output-cs>");
    Console.Error.WriteLine("   or: MissionPlanner.MavLink.Generator enums <repository-root> <output-cs>");
    Console.Error.WriteLine("   or: MissionPlanner.MavLink.Generator wire <repository-root> <models-output-cs> <decoders-output-cs>");
    Console.Error.WriteLine("   or: MissionPlanner.MavLink.Generator promotion <repository-root> <output-json>");
    return 1;
}

var coverageManifest = LoadManifest(args[0]);
var report = MavLinkCoverageAnalyzer.Create(args[0], coverageManifest.SourceRevision);
var output = args.Length == 2 ? args[1] : Path.Combine(args[0], "artifacts", "mavlink-coverage.json");
MavLinkCoverageAnalyzer.Write(report, output);
Console.WriteLine($"Wrote {report.Messages.Count} message entries to {output}.");
return 0;

static MavLinkGenerationManifest LoadManifest(string repositoryRoot)
{
    var path = Path.Combine(
        repositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        "mavlink-generation.json");
    var manifest = JsonSerializer.Deserialize<MavLinkGenerationManifest>(
        File.ReadAllText(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return manifest ?? throw new InvalidDataException($"MAVLink generation manifest is empty: {path}");
}

internal sealed record MavLinkGenerationManifest(string SourceRevision, string RootDialect);
