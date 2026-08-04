using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Downloads;

namespace MissionPlanner.Firmware.Tests;

public sealed class FileSystemFirmwareArtifactStoreTests
{
    [Fact]
    public async Task CommitPublishesDataAndMetadataTogether()
    {
        var store = CreateStore(out _);
        await using var writer = await store.CreateTemporaryAsync("one", TestContext.Current.CancellationToken);
        await writer.Stream.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        var metadata = Metadata("one", 3, DateTimeOffset.UtcNow);
        await writer.CommitAsync(metadata, TestContext.Current.CancellationToken);
        var entries = await store.EnumerateAsync(TestContext.Current.CancellationToken);
        entries.Should().ContainSingle().Which.Metadata.Should().Be(metadata);
        (await store.RemoveAsync("one", TestContext.Current.CancellationToken)).Should().BeTrue();
        (await store.TryGetAsync("one", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task CleanupEnforcesQuotaNewestFirst()
    {
        var store = CreateStore(out _, quota: 3);
        await CommitAsync(store, "old", DateTimeOffset.UtcNow.AddMinutes(-1));
        await CommitAsync(store, "new", DateTimeOffset.UtcNow);
        await store.CleanupAsync(TestContext.Current.CancellationToken);
        (await store.TryGetAsync("old", TestContext.Current.CancellationToken)).Should().BeNull();
        (await store.TryGetAsync("new", TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    private static async Task CommitAsync(FileSystemFirmwareArtifactStore store, string key, DateTimeOffset time)
    {
        await using var writer = await store.CreateTemporaryAsync(key, TestContext.Current.CancellationToken);
        await writer.Stream.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await writer.CommitAsync(Metadata(key, 3, time), TestContext.Current.CancellationToken);
    }

    private static FileSystemFirmwareArtifactStore CreateStore(out TestPaths paths, long quota = 1024)
    {
        paths = new TestPaths();
        return new(paths, Options.Create(new FirmwareOptions { ArtifactCacheQuotaBytes = quota }), TimeProvider.System);
    }

    private static FirmwareArtifactMetadata Metadata(string key, long size, DateTimeOffset time) =>
        new(key, new Uri("https://example.test/fw.apj"), time, size, new string('A', 64));

    private sealed class TestPaths : IFirmwareCachePathProvider
    {
        public string CacheRoot { get; } = Path.Combine(Path.GetTempPath(), "MissionPlannerFirmwareStoreTests", Guid.NewGuid().ToString("N"));
    }
}
