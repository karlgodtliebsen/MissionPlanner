using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Logs;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Logging;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class ApplicationLogsViewModelTests
{
    [Fact]
    public async Task LiveBatchesPauseClearAndHiddenViewsDoNotStopTheLogger()
    {
        var buffer = new ApplicationLogBuffer(100);
        var storage = new BrowserLogStorage();
        var state = new ApplicationLogFileState(new ConfigurationBuilder().Build());
        var levels = new ApplicationLogLevelController(new LoggingLevelSwitch(LogEventLevel.Information));
        var dispatcher = new InlineDispatcher();
        using var model = new ApplicationLogsViewModel(buffer, levels, new ApplicationLogHistory(storage, state),
            state, storage, Substitute.For<IFileSaveService>(), Substitute.For<ITextClipboardService>(),
            Substitute.For<ILogFolderService>(), NullLogger<ApplicationLogsViewModel>.Instance,
            dispatcher, Substitute.For<IDomainEventHub>());
        using var logger = new LoggerConfiguration().WriteTo.Sink(buffer).CreateLogger();
        await model.ActivateAsync();
        model.Paused = true;
        for (var i = 0; i < 10000; i++)
        {
            logger.Information("Event {Index}", i);
        }

        model.RefreshLiveView();
        Assert.Empty(model.Entries);
        Assert.Equal(100, buffer.Count);
        var batches = 0;
        model.Entries.CollectionChanged += (_, _) => batches++;
        model.Paused = false;
        Assert.Equal(100, model.Entries.Count);
        Assert.InRange(batches, 1, 2);
        model.ClearViewCommand.Execute(null);
        Assert.Empty(model.Entries);
        Assert.Equal(100, buffer.Count);
        logger.Warning("New warning");
        model.RefreshLiveView();
        Assert.Equal("New warning", Assert.Single(model.Entries).RenderedMessage);
        model.RuntimeLevel = LogEventLevel.Verbose;
        Assert.True(model.IsVerbose);
        Assert.Equal(LogEventLevel.Verbose, levels.MinimumLevel);
        await model.DeactivateAsync();
        logger.Error("After hiding");
        Assert.Equal(100, buffer.Count);
        await model.ActivateAsync();
        Assert.Equal(2, model.Entries.Count);
        model.MinimumLevel = LogEventLevel.Error;
        model.ApplyFiltersCommand.Execute(null);
        model.SelectedEntry = Assert.Single(model.Entries);
        Assert.Contains("After hiding", model.Details);
        Assert.False(model.HasFileLogging);
        Assert.False(model.CanOpenFolder);
        await model.DeactivateAsync();
    }

    [Fact]
    public async Task SnapshotIncludesHiddenEventsStructuredPropertiesAndExceptions()
    {
        var buffer = new ApplicationLogBuffer(100);
        var storage = new BrowserLogStorage();
        var state = new ApplicationLogFileState(new ConfigurationBuilder().Build());
        var clipboard = Substitute.For<ITextClipboardService>();
        string? copied = null;
        clipboard.SetTextAsync(Arg.Any<string>()).Returns(call =>
        {
            copied = call.Arg<string>();
            return Task.CompletedTask;
        });
        using var model = new ApplicationLogsViewModel(buffer,
            new ApplicationLogLevelController(new LoggingLevelSwitch(LogEventLevel.Information)),
            new ApplicationLogHistory(storage, state), state, storage, Substitute.For<IFileSaveService>(),
            clipboard, Substitute.For<ILogFolderService>(), NullLogger<ApplicationLogsViewModel>.Instance,
            new InlineDispatcher(), Substitute.For<IDomainEventHub>());
        using var logger = new LoggerConfiguration().WriteTo.Sink(buffer).CreateLogger();
        logger.Information("First {@Data}", new { Nested = new { Value = 42 }, Items = new[] { 1, 2 } });
        model.RefreshLiveView();
        model.SelectedEntry = Assert.Single(model.Entries);
        model.ClearViewCommand.Execute(null);
        model.Paused = true;
        logger.Error(new InvalidOperationException("outer", new Exception("inner")), "Failure {Code}", 17);
        model.Search = "no match";
        model.ApplyFiltersCommand.Execute(null);
        await model.CopySnapshotCommand.ExecuteAsync(null);
        Assert.Empty(model.Entries);
        Assert.Null(model.ErrorMessage);
        using var json = System.Text.Json.JsonDocument.Parse(copied!);
        var events = json.RootElement.GetProperty("Events");
        Assert.Equal(2, events.GetArrayLength());
        Assert.Contains("inner", events[0].GetProperty("Exception").GetString());
        Assert.Equal(17, events[0].GetProperty("Properties").GetProperty("Code").GetInt32());
        Assert.Equal(42, events[1].GetProperty("Properties").GetProperty("Data").GetProperty("Nested").GetProperty("Value").GetInt32());
        Assert.Equal(2, events[1].GetProperty("Properties").GetProperty("Data").GetProperty("Items").GetArrayLength());
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Dispatch(Action action) => action();
        public T Dispatch<T>(Func<T> action) => action();
        public Task DispatchAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> DispatchAsync<T>(Func<T> action) => Task.FromResult(action());
        public Task DispatchAsync(Func<Task> action) => action();
        public Task<T> DispatchAsync<T>(Func<Task<T>> action) => action();
    }
}
