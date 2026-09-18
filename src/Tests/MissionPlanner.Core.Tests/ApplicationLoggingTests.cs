using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MissionPlanner.Core.Tests;

public sealed class ApplicationLoggingTests
{
    [Fact]
    public void BufferRetainsNewestEventsInOrderAndStructuredValues()
    {
        var buffer = new ApplicationLogBuffer(2);
        using var logger = new LoggerConfiguration().WriteTo.Sink(buffer).CreateLogger();
        logger.Information("Old");
        var exception = new InvalidOperationException("test");
        logger.ForContext("SourceContext", "Transport").Error(exception, "Failure {Count}", 42);
        logger.Warning("Last");
        var entries = buffer.Snapshot();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Failure {Count}", entries[0].MessageTemplate);
        Assert.Equal("Failure 42", entries[0].RenderedMessage);
        Assert.Equal("Transport", entries[0].SourceContext);
        Assert.Same(exception, entries[0].Exception);
        Assert.Equal(42, ((ScalarValue)entries[0].Properties["Count"]).Value);
        Assert.True(entries[0].Sequence < entries[1].Sequence);
    }

    [Fact]
    public async Task SubscriberExceptionsDoNotEscapeOrBlockLogging()
    {
        var buffer = new ApplicationLogBuffer();
        var notification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        buffer.Changed += (_, _) => throw new InvalidOperationException("viewer failed");
        buffer.Changed += (_, _) => notification.TrySetResult();
        using var logger = new LoggerConfiguration().WriteTo.Sink(buffer).CreateLogger();
        logger.Information("Still works");
        await notification.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Single(buffer.Snapshot());
    }

    [Fact]
    public void RuntimeSwitchUsesConfiguredLevelAndChangesWithoutRestart()
    {
        var configuration = Config(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Warning"
        });
        using var services = new ServiceCollection().AddLogging(configuration).BuildServiceProvider();
        var logger = services.GetRequiredService<Serilog.ILogger>();
        var buffer = services.GetRequiredService<ApplicationLogBuffer>();
        var levels = services.GetRequiredService<IApplicationLogLevelController>();
        Assert.Equal(LogEventLevel.Warning, levels.MinimumLevel);
        logger.Debug("Filtered");
        levels.SetMinimumLevel(LogEventLevel.Debug);
        logger.Debug("Visible");
        Assert.Equal("Visible", Assert.Single(buffer.Snapshot()).RenderedMessage);
    }

    [Fact]
    public void ResolverPreservesRollingSettingsAndReplacesOnlyPlaceholder()
    {
        var root = Path.Combine(Path.GetTempPath(), "MissionPlanner-logging-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = FileConfig();
            var paths = new DesktopLogPathProvider(_ => root);
            var resolved = ApplicationLogConfiguration.Resolve(configuration, paths, false);
            Assert.Equal(Path.Combine(paths.ApplicationLogDirectory, "MissionPlanner.NextGen.Application-.log"),
                resolved["Serilog:WriteTo:0:Args:path"]);
            Assert.Equal("Day", resolved["Serilog:WriteTo:0:Args:rollingInterval"]);
            Assert.Equal("7", resolved["Serilog:WriteTo:0:Args:retainedFileCountLimit"]);
            Assert.Equal("{ApplicationLogPath}", configuration["Serilog:WriteTo:0:Args:path"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void BrowserRemovesFileSinkAndAssemblyWithoutResolvingPaths()
    {
        var resolved = ApplicationLogConfiguration.Resolve(FileConfig(), null, true);
        Assert.DoesNotContain(resolved.AsEnumerable(), pair => pair.Value == "File" || pair.Value == "Serilog.Sinks.File");
        Assert.Equal("Day", FileConfig()["Serilog:WriteTo:0:Args:rollingInterval"]);
        using var logger = new LoggerConfiguration().ReadFrom.Configuration(resolved).CreateLogger();
    }

    [Fact]
    public async Task ConcurrentProducersStayBoundedAndOrdered()
    {
        var buffer = new ApplicationLogBuffer(100);
        using var logger = new LoggerConfiguration().WriteTo.Sink(buffer).CreateLogger();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                logger.Information("Event {Index}", i);
            }
        }, TestContext.Current.CancellationToken)));
        var snapshot = buffer.Snapshot();
        Assert.Equal(100, snapshot.Count);
        Assert.Equal(8000, snapshot[^1].Sequence);
        Assert.True(snapshot.Zip(snapshot.Skip(1)).All(pair => pair.First.Sequence < pair.Second.Sequence));
    }

    private static IConfiguration FileConfig() => Config(new()
    {
        ["Serilog:Using:0"] = "Serilog.Sinks.File",
        ["Serilog:WriteTo:0:Name"] = "File",
        ["Serilog:WriteTo:0:Args:path"] = "{ApplicationLogPath}",
        ["Serilog:WriteTo:0:Args:rollingInterval"] = "Day",
        ["Serilog:WriteTo:0:Args:retainedFileCountLimit"] = "7"
    });

    private static IConfiguration Config(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
