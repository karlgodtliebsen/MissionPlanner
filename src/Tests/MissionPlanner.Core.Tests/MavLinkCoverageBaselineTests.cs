using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MissionPlanner.MavLink.Generator;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates the deterministic MAVLink dialect coverage baseline.
/// </summary>
public sealed class MavLinkCoverageBaselineTests
{
    private const string SourceRevision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";

    /// <summary>Verifies inherited definitions have unique IDs and names.</summary>
    [Fact]
    public void DialectInheritanceHasNoConflictingMessages()
    {
        var definitions = LoadDefinitions();
        definitions.Select(definition => definition.MessageId).Should().OnlyHaveUniqueItems();
        definitions.Select(definition => definition.Name).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Verifies representative CRC and payload values against the official dialect.</summary>
    [Theory]
    [InlineData(0, "HEARTBEAT", 50, 9, 9)]
    [InlineData(24, "GPS_RAW_INT", 24, 30, 52)]
    [InlineData(148, "AUTOPILOT_VERSION", 178, 60, 78)]
    [InlineData(163, "AHRS", 127, 28, 28)]
    [InlineData(11030, "ESC_TELEMETRY_1_TO_4", 144, 44, 44)]
    public void ComputesOfficialWireDefinitions(uint id, string name, byte crc, byte minimumLength, byte maximumLength)
    {
        var definition = LoadDefinitions().Single(item => item.MessageId == id);
        definition.Should().Be(new DialectMessageDefinition(id, name, crc, minimumLength, maximumLength, definition.Dialect, definition.IsDeprecated));
    }

    /// <summary>Verifies current constants either agree with the dialect or are explicitly baselined.</summary>
    [Fact]
    public void CurrentHandWrittenDefinitionsMatchOfficialDialectExceptDocumentedLegacyConstant()
    {
        var report = CreateReport();
        report.IncorrectConstants.Should().Equal("MissionChanged=52");
        report.IncorrectCrcExtras.Should().BeEmpty();
    }

    /// <summary>Verifies every current typed decoder is represented in the official catalog.</summary>
    [Fact]
    public void CurrentDecodersUseOfficialMessageIds()
    {
        var report = CreateReport();
        var decodedEntries = report.Messages.Where(entry => entry.TypedDecoderExists).ToArray();
        decodedEntries.Should().NotBeEmpty();
        report.UnknownDecoderMessageIds.Should().BeEmpty();
    }

    /// <summary>Verifies report generation is byte-for-byte stable.</summary>
    [Fact]
    public void CoverageReportIsDeterministic()
    {
        var first = JsonSerializer.Serialize(CreateReport(), JsonOptions());
        var second = JsonSerializer.Serialize(CreateReport(), JsonOptions());
        second.Should().Be(first);
    }

    private static IReadOnlyList<DialectMessageDefinition> LoadDefinitions() =>
        MavLinkDialectLoader.Load(Path.Combine(RepositoryRoot(), "src", "Core", "MissionPlanner.MavLink", "Dialects", "ardupilotmega.xml"));

    private static MavLinkCoverageReport CreateReport() => MavLinkCoverageAnalyzer.Create(RepositoryRoot(), SourceRevision);

    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "MissionPlanner.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the MissionPlanner repository root.");
    }
}
