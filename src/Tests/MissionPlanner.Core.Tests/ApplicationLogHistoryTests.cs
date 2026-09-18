using System.Text;
using Microsoft.Extensions.Configuration;
using MissionPlanner.Library.Logging;
using Serilog;
using Serilog.Events;

namespace MissionPlanner.Core.Tests;

public sealed class ApplicationLogHistoryTests
{
    [Fact]
    public async Task ReadsHistoricalTextWithSourceAndExceptionAndToleratesMalformedTail()
    {
        var storage = new BrowserLogStorage();
        await using (var writer = await storage.CreateAsync(LogStorageArea.Application, "old.log", TestContext.Current.CancellationToken))
        {
            var text = "2026-09-18 12:00:00.000 +00:00 [INF] (1) [Transport] Connected\n" +
                "2026-09-18 12:00:01.000 +00:00 [ERR] (2) [Firmware] Failed\n" +
                "System.InvalidOperationException: Test failure\n   at Test.Method()\n2026-09-18 12:";
            await writer.WriteAsync(Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken);
        }

        var history = new ApplicationLogHistory(storage, new ApplicationLogFileState(new ConfigurationBuilder().Build()));
        Assert.Single(await history.ListAsync(TestContext.Current.CancellationToken));
        var rows = await history.ReadAsync("old.log", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Transport", rows[0].SourceContext);
        Assert.Equal(LogEventLevel.Error, rows[1].Level);
        Assert.Contains("Test.Method", ApplicationLogFilter.Details(rows[1]));
        Assert.True(new ApplicationLogFilter(ExceptionOnly: true).Matches(rows[1]));
        Assert.Single(await history.ReadAsync("old.log", 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsActiveFilesBeforeCallingStorageDeletion()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:WriteTo:0:Name"] = "File",
            ["Serilog:WriteTo:0:Args:path"] = "{ApplicationLogPath}",
            ["Serilog:WriteTo:0:Args:rollingInterval"] = "Day"
        }).Build();
        var paths = NSubstitute.Substitute.For<ILogPathProvider>();
        var state = new ApplicationLogFileState(configuration, paths);
        var name = $"MissionPlanner.NextGen.Application-{DateTime.Now:yyyyMMdd}.log";
        var storage = new BrowserLogStorage();
        await using (await storage.CreateAsync(LogStorageArea.Application, name, TestContext.Current.CancellationToken))
        {
        }

        var file = Assert.Single(await storage.ListAsync(LogStorageArea.Application, TestContext.Current.CancellationToken));
        var history = new ApplicationLogHistory(storage, state);
        Assert.True(state.IsActive(file));
        await Assert.ThrowsAsync<IOException>(() => history.DeleteAsync(file, TestContext.Current.CancellationToken));
        Assert.Single(await history.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StructuredFiltersCombineSeverityCategoryTimeAndException()
    {
        var buffer = new ApplicationLogBuffer();
        using var logger = new LoggerConfiguration().WriteTo.Sink(buffer).CreateLogger();
        logger.ForContext("SourceContext", "MissionPlanner.Transport.Serial")
            .Error(new IOException("Disconnected"), "Failure {VehicleId}", "vehicle-1");
        var row = Assert.Single(buffer.Snapshot());
        Assert.True(new ApplicationLogFilter(LogEventLevel.Warning, Source: "Transport",
            Search: "vehicle-1", From: row.Timestamp, Until: row.Timestamp, ExceptionOnly: true).Matches(row));
        Assert.False(new ApplicationLogFilter(Exact: LogEventLevel.Warning).Matches(row));
        Assert.False(new ApplicationLogFilter(Source: "Firmware").Matches(row));
    }
}
