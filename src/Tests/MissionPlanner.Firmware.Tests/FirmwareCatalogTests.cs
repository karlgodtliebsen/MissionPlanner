using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareCatalogTests
{
    [Fact]
    public void ParserNormalizesGzipDeduplicatesAndPreservesUnknownMetadata()
    {
        var parser = CreateParser();

        var entries = parser.Parse(Gzip(FixtureBytes()));

        entries.Should().HaveCount(4);
        var stable = entries.Single(entry => entry.Target.BoardId == 50 && entry.Channel == FirmwareReleaseChannel.Stable);
        stable.Target.UsbIdentifiers.Should().ContainSingle().Which.Should().Be(new UsbIdentifier(0x2dae, 0x1016));
        stable.RawMetadata.Should().ContainKey("future-field");
        entries.Select(entry => entry.Channel).Should().Contain([FirmwareReleaseChannel.Stable, FirmwareReleaseChannel.Beta, FirmwareReleaseChannel.Latest]);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"firmware\":true}")]
    public void ParserRejectsCorruptJson(string value)
    {
        var act = () => CreateParser().Parse(System.Text.Encoding.UTF8.GetBytes(value));
        act.Should().Throw<FirmwareManifestException>();
    }

    [Fact]
    public void ParserRejectsCorruptGzip()
    {
        var act = () => CreateParser().Parse(new byte[] { 0x1f, 0x8b, 1, 2, 3 });
        act.Should().Throw<FirmwareManifestException>();
    }

    [Fact]
    public void ParserSupportsCurrentOfficialManifestFieldNames()
    {
        const string json = """
                            {"firmware":[{"vehicletype":"AntennaTracker","platform":"BeastH7","url":"https://firmware.ardupilot.org/AntennaTracker/stable-4.7.0/BeastH7/antennatracker.apj","format":"apj","mav-firmware-version":"4.7.0","mav-firmware-version-type":"STABLE-4.7.0","board_id":1025,"image_size":807112,"USBID":["0x1209/0x5741"],"bootloader_str":["BeastH7-BL"]}]}
                            """;

        var entry = CreateParser().Parse(System.Text.Encoding.UTF8.GetBytes(json)).Should().ContainSingle().Subject;

        entry.Channel.Should().Be(FirmwareReleaseChannel.Stable);
        entry.Artifact.Size.Should().BeNull("the official manifest does not report encoded APJ length");
        entry.Artifact.ImageSize.Should().Be(807112);
        entry.Target.UsbIdentifiers.Should().Contain(new UsbIdentifier(0x1209, 0x5741));
    }

    [Fact]
    public void ParserSkipsMalformedEntryAndReportsReason()
    {
        const string json = """
            {"firmware":[
              {"vehicletype":"Copter","platform":"Good","url":"https://example.test/good.apj","format":"apj","version":"1.0","board_id":50,"future":"kept"},
              {"vehicletype":"Copter","platform":"BadUsb","url":"https://example.test/bad.apj","format":"apj","version":"1.0","board_id":51,"USBID":["wrong"]}
            ]}
            """;
        var result = CreateParser().ParseWithDiagnostics(System.Text.Encoding.UTF8.GetBytes(json));
        result.Entries.Should().ContainSingle().Which.RawMetadata.Should().ContainKey("future");
        result.Diagnostics.Should().BeEquivalentTo(new { TotalEntries = 2, AcceptedEntries = 1, SkippedEntries = 1 });
        result.Diagnostics.SkipReasons.Should().Contain("invalid-usb-id", 1);
    }

    [Fact]
    public void ParserFailsWhenAllEntriesAreMalformed()
    {
        const string json = """{"firmware":[{"platform":"Missing board and URL"}]}""";
        var act = () => CreateParser().Parse(System.Text.Encoding.UTF8.GetBytes(json));
        act.Should().Throw<FirmwareManifestException>().WithMessage("*no usable*");
    }

    [Fact]
    public void ParserDeduplicatesMirrorEntriesWithDiagnostic()
    {
        const string json = """
            {"firmware":[
              {"vehicletype":"Copter","platform":"Board","url":"https://one.test/fw.apj","format":"apj","version":"1.0","board_id":50},
              {"vehicletype":"Copter","platform":"Board","url":"https://two.test/fw.apj","format":"apj","version":"1.0","board_id":50}
            ]}
            """;
        var result = CreateParser().ParseWithDiagnostics(System.Text.Encoding.UTF8.GetBytes(json));
        result.Entries.Should().ContainSingle();
        result.Diagnostics.SkipReasons.Should().Contain("duplicate-mirror", 1);
    }

    [Fact]
    public async Task CatalogFiltersDeterministicallyWithoutNetworkWhenCacheIsFresh()
    {
        var cache = new MemoryFirmwareCatalogCache();
        var now = DateTimeOffset.Parse("2026-08-03T10:00:00Z");
        await cache.SetAsync(new CachedFirmwareManifest(FixtureBytes(), now), TestContext.Current.CancellationToken);
        var client = new StubClient { Exception = new InvalidOperationException("Network must not be used.") };
        var service = CreateService(client, cache, new FixedTimeProvider(now));

        var catalog = await service.GetCatalogAsync(new FirmwareCatalogRequest(
            FirmwareVehicleType.Copter,
            FirmwareReleaseChannel.Stable,
            UsbIdentifier: new UsbIdentifier(0x2dae, 0x1016)), TestContext.Current.CancellationToken);

        catalog.Entries.Should().ContainSingle();
        client.CallCount.Should().Be(0);
        catalog.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task NetworkFailureFallsBackToStaleValidCache()
    {
        var cache = new MemoryFirmwareCatalogCache();
        var old = DateTimeOffset.Parse("2026-08-01T10:00:00Z");
        await cache.SetAsync(new CachedFirmwareManifest(FixtureBytes(), old, "\"etag\""), TestContext.Current.CancellationToken);
        var client = new StubClient { Exception = new HttpRequestException("offline") };
        var service = CreateService(client, cache, new FixedTimeProvider(old.AddDays(2)));

        var catalog = await service.GetCatalogAsync(new FirmwareCatalogRequest(BoardId: 9), TestContext.Current.CancellationToken);

        catalog.Entries.Should().ContainSingle().Which.Target.VehicleType.Should().Be(FirmwareVehicleType.Plane);
        catalog.IsStale.Should().BeTrue();
        client.CallCount.Should().Be(1);
    }

    private static FirmwareCatalogService CreateService(IFirmwareManifestClient client, IFirmwareCatalogCache cache, TimeProvider clock)
    {
        var options = Options.Create(new FirmwareOptions { CatalogCacheDuration = TimeSpan.FromHours(1) });
        return new FirmwareCatalogService(client, CreateParser(), cache, options, clock, NullLogger<FirmwareCatalogService>.Instance);
    }

    private static ArduPilotFirmwareManifestParser CreateParser()
    {
        return new ArduPilotFirmwareManifestParser(Options.Create(new FirmwareOptions()));
    }

    private static byte[] FixtureBytes()
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "manifest.json"));
    }

    private static byte[] Gzip(byte[] input)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
        {
            gzip.Write(input);
        }

        return output.ToArray();
    }

    private sealed class StubClient : IFirmwareManifestClient
    {
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }

        public Task<FirmwareManifestResponse> GetAsync(Uri uri, CachedFirmwareManifest? cached, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Exception is null
                ? Task.FromResult(new FirmwareManifestResponse(FixtureBytes(), false))
                : Task.FromException<FirmwareManifestResponse>(Exception);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
