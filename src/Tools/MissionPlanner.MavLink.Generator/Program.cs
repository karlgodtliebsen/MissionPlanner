using MissionPlanner.MavLink.Generator;

const string revision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";
if (args is ["registry", var repositoryRoot, var registryOutput])
{
    var definitions = MavLinkDialectLoader.Load(Path.Combine(
        repositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        "ardupilotmega.xml"));
    var source = MavLinkRegistrySourceGenerator.Generate(definitions, revision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(registryOutput))!);
    File.WriteAllText(registryOutput, source);
    Console.WriteLine($"Wrote {definitions.Count} message definitions to {registryOutput}.");
    return 0;
}

if (args is ["enums", var enumRepositoryRoot, var enumOutput])
{
    var definitions = MavLinkDialectEnumLoader.Load(Path.Combine(
        enumRepositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        "ardupilotmega.xml"));
    var source = MavLinkEnumSourceGenerator.Generate(definitions, revision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(enumOutput))!);
    File.WriteAllText(enumOutput, source);
    Console.WriteLine($"Wrote {definitions.Count} protocol enums to {enumOutput}.");
    return 0;
}

if (args is ["wire", var wireRepositoryRoot, var modelOutput, var decoderOutput])
{
    var definitions = MavLinkDialectWireLoader.Load(Path.Combine(
        wireRepositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        "ardupilotmega.xml"));
    var modelSource = MavLinkWireModelSourceGenerator.GenerateModels(definitions, revision);
    var decoderSource = MavLinkWireModelSourceGenerator.GenerateDecoders(definitions, revision);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(modelOutput))!);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(decoderOutput))!);
    File.WriteAllText(modelOutput, modelSource);
    File.WriteAllText(decoderOutput, decoderSource);
    var generatedCount = definitions.Count(definition =>
        (!definition.IsDeprecated || definition.Name is "BATTERY2" or "HWSTATUS" or "MISSION_ITEM" or "MISSION_REQUEST")
        && !MavLinkWireModelSourceGenerator.HandWrittenOverrides.Contains(definition.Name));
    Console.WriteLine($"Wrote {generatedCount} wire models and decoders to {modelOutput} and {decoderOutput}.");
    return 0;
}

if (args is ["promotion", var promotionRepositoryRoot, var promotionOutput])
{
    var definitions = MavLinkDialectLoader.Load(Path.Combine(
        promotionRepositoryRoot,
        "src",
        "Core",
        "MissionPlanner.MavLink",
        "Dialects",
        "ardupilotmega.xml"));
    var catalog = MavLinkPromotionCatalogGenerator.Generate(definitions, revision);
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

var report = MavLinkCoverageAnalyzer.Create(args[0], revision);
var output = args.Length == 2 ? args[1] : Path.Combine(args[0], "artifacts", "mavlink-coverage.json");
MavLinkCoverageAnalyzer.Write(report, output);
Console.WriteLine($"Wrote {report.Messages.Count} message entries to {output}.");
return 0;
