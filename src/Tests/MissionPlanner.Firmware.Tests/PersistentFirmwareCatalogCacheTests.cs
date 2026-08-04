using FluentAssertions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Configuration;

namespace MissionPlanner.Firmware.Tests;

public sealed class PersistentFirmwareCatalogCacheTests
{
    [Fact]
    public async Task FreshInstanceLoadsPersistedValidatorsAndContent()
    {
        var paths = new TestPaths();
        var expected = new CachedFirmwareManifest(new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow, "etag", DateTimeOffset.UtcNow, new Uri("https://example.test/manifest.json"));
        await new PersistentFirmwareCatalogCache(paths).SetAsync(expected, TestContext.Current.CancellationToken);
        var actual = await new PersistentFirmwareCatalogCache(paths).GetAsync(TestContext.Current.CancellationToken);
        actual.Should().NotBeNull();
        actual!.Content.ToArray().Should().Equal(1, 2, 3);
        actual.ETag.Should().Be("etag");
        actual.SourceUri.Should().Be(expected.SourceUri);
    }

    [Fact]
    public async Task CorruptPersistentEntryIsDiscarded()
    {
        var paths = new TestPaths();
        var directory = Path.Combine(paths.CacheRoot, "catalog");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest-cache.json"), "not-json", TestContext.Current.CancellationToken);
        (await new PersistentFirmwareCatalogCache(paths).GetAsync(TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentReadersObserveCommittedValue()
    {
        var paths = new TestPaths();
        var cache = new PersistentFirmwareCatalogCache(paths);
        await cache.SetAsync(new CachedFirmwareManifest(new byte[] { 9 }, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.GetAsync(TestContext.Current.CancellationToken)));
        results.Should().NotContainNulls();
        foreach (var result in results)
        {
            result!.Content.ToArray().Should().Equal(9);
        }
    }

    private sealed class TestPaths : IFirmwareCachePathProvider
    {
        public string CacheRoot { get; } = Path.Combine(Path.GetTempPath(), "MissionPlannerFirmwareTests", Guid.NewGuid().ToString("N"));
    }
}
