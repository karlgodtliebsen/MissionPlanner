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
