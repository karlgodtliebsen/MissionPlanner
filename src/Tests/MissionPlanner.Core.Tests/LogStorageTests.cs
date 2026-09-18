using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.Core.Tests;

public sealed class LogStorageTests : IDisposable
{
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "MissionPlanner-logs-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PathsAreLazyAndApplicationIsSeparate()
    {
        var provider = new DesktopLogPathProvider(_ => temporary);
        Assert.False(Directory.Exists(temporary));
        Assert.Equal(Path.Combine(temporary, "Mission Planner", "logs"), provider.LogsRoot);
        Assert.Equal(provider.LogsRoot, provider.TelemetryDirectory);
        Assert.Equal(Path.Combine(provider.LogsRoot, "application"), provider.ApplicationLogDirectory);
        Assert.True(Directory.Exists(provider.LogsRoot));
        Assert.False(Directory.Exists(provider.ApplicationLogDirectory));
    }

    [Theory]
    [InlineData(Environment.SpecialFolder.LocalApplicationData)]
    [InlineData(Environment.SpecialFolder.UserProfile)]
    public void MissingDocumentsFallsBack(Environment.SpecialFolder available)
    {
        var provider = new DesktopLogPathProvider(folder => folder == available ? temporary : "");
        Assert.Equal(Path.Combine(temporary, "Mission Planner", "logs"), provider.LogsRoot);
    }

    [Fact]
    public void UnwritableDocumentsFallsBack()
    {
        Directory.CreateDirectory(temporary);
        var blocked = Path.Combine(temporary, "blocked");
        File.WriteAllText(blocked, "not a directory");
        var provider = new DesktopLogPathProvider(folder =>
            folder == Environment.SpecialFolder.MyDocuments ? blocked : temporary);
        Assert.Equal(Path.Combine(temporary, "Mission Planner", "logs"), provider.LogsRoot);
    }

    [Fact]
    public void MissingAllLocationsReportsClearFailure()
    {
        var provider = new DesktopLogPathProvider(_ => "");
        Assert.Contains("No writable logging location", Assert.Throws<IOException>(() => provider.LogsRoot).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageSupportsExclusiveCreationReadExportAndDelete(bool browser)
    {
        ILogStorage storage = browser ? new BrowserLogStorage() : new DesktopLogStorage(new DesktopLogPathProvider(_ => temporary));
        Assert.Empty(await storage.ListAsync(LogStorageArea.Application));
        await using (var writer = await storage.CreateAsync(LogStorageArea.Telemetry, "flight.tlog"))
        {
            await writer.WriteAsync(new byte[] { 1, 2, 3 });
            await writer.FlushAsync();
            await Assert.ThrowsAsync<IOException>(() => storage.CreateAsync(LogStorageArea.Telemetry, "flight.tlog"));
            await Assert.ThrowsAsync<IOException>(() => storage.DeleteAsync(LogStorageArea.Telemetry, "flight.tlog"));
        }

        var item = Assert.Single(await storage.ListAsync(LogStorageArea.Telemetry));
        Assert.Equal(3, item.Size);
        Assert.Equal(browser, item.PhysicalPath is null);
        await using (var export = await storage.ExportAsync(LogStorageArea.Telemetry, item.Id))
        {
            Assert.Equal("flight.tlog", export.FileName);
            using var content = new MemoryStream();
            await export.Content.CopyToAsync(content);
            Assert.Equal(new byte[] { 1, 2, 3 }, content.ToArray());
        }

        await storage.DeleteAsync(LogStorageArea.Telemetry, item.Id);
        Assert.Empty(await storage.ListAsync(LogStorageArea.Telemetry));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("C:\\escape")]
    [InlineData("/escape")]
    [InlineData("..")]
    [InlineData("bad:stream")]
    public async Task RejectsTraversalOnBothPlatforms(string name)
    {
        foreach (ILogStorage storage in new ILogStorage[] { new BrowserLogStorage(), new DesktopLogStorage(new DesktopLogPathProvider(_ => temporary)) })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => storage.CreateAsync(LogStorageArea.Telemetry, name));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(LogStorageArea.Telemetry, name));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(LogStorageArea.Telemetry, name));
        }

        Assert.False(Directory.Exists(temporary));
    }

    [Fact]
    public async Task BrowserQuotaDoesNotEvictLogsAndDeletionReclaimsBytes()
    {
        var storage = new BrowserLogStorage(3, 1);
        await using (var writer = await storage.CreateAsync(LogStorageArea.Telemetry, "one.tlog"))
        {
            await writer.WriteAsync(new byte[] { 1, 2, 3 });
            await Assert.ThrowsAsync<IOException>(async () => await writer.WriteAsync(new byte[] { 4 }));
            await Assert.ThrowsAsync<IOException>(() => storage.CreateAsync(LogStorageArea.Telemetry, "two.tlog"));
        }

        Assert.Equal(3, Assert.Single(await storage.ListAsync(LogStorageArea.Telemetry)).Size);
        await storage.DeleteAsync(LogStorageArea.Telemetry, "one.tlog");
        await using var next = await storage.CreateAsync(LogStorageArea.Telemetry, "two.tlog");
        await next.WriteAsync(new byte[] { 5, 6, 7 });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegistersPlatformWithoutTouchingDisk(bool browser)
    {
        var services = new ServiceCollection().AddLogStorage(browser);
        using var provider = services.BuildServiceProvider();
        Assert.Equal(browser ? typeof(BrowserLogStorage) : typeof(DesktopLogStorage), provider.GetRequiredService<ILogStorage>().GetType());
        if (browser)
        {
            Assert.Null(provider.GetService<ILogPathProvider>());
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(temporary))
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
