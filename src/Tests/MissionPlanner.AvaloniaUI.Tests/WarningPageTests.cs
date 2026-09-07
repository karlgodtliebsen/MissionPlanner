using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced.Warnings;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;
using MissionPlanner.App.Presentation;
using System.Text;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class WarningPageTests
{
    [Fact]
    public async Task RepeatedNavigationDoesNotDuplicateSaveHandlersAndDetachedEditorCannotPersist()
    {
        var store = new Store();
        var (page, editor) = Create(store);
        using (page)
        {
            for (var cycle = 0; cycle < 10; cycle++)
            {
                await page.ActivateAsync();
                await page.ActivateAsync();
                editor.Name = "Rule " + cycle;
                editor.SaveCommand.Execute(null);
                Assert.Equal(cycle + 1, store.Writes);
                Assert.Equal(cycle + 1, page.Rules.Rules.Count);
                await page.DeactivateAsync();
                editor.SaveCommand.Execute(null);
                Assert.Equal(cycle + 1, store.Writes);
                Assert.Empty(page.Status.Warnings);
            }
        }
    }

    [Fact]
    public async Task DeactivationCancelsPendingLoadWithoutLateSubscriptions()
    {
        var store = new Store { BlockReads = true };
        var (page, editor) = Create(store);
        using (page)
        {
            var activation = page.ActivateAsync();
            Assert.False(activation.IsCompleted);
            await page.DeactivateAsync();
            await activation;
            editor.Edit(null);
            editor.SaveCommand.Execute(null);
            Assert.Equal(0, store.Writes);
            store.BlockReads = false;
            await page.ActivateAsync();
            editor.SaveCommand.Execute(null);
            Assert.Equal(1, store.Writes);
            await page.DeactivateAsync();
        }
    }

    [Fact]
    public async Task PreviewAndInvalidEditsNeverSaveOrChangeLiveRules()
    {
        var store = new Store();
        var (page, editor) = Create(store);
        using (page)
        {
            await page.ActivateAsync();
            editor.PreviewCommand.Execute(null);
            Assert.NotEmpty(editor.PreviewResult);
            Assert.Equal(0, store.Writes);
            editor.Name = "";
            editor.SaveCommand.Execute(null);
            Assert.NotEmpty(editor.NameError);
            Assert.Empty(page.Rules.Rules);
            await page.DeactivateAsync();
        }
    }

    [Fact]
    public async Task ExportAndImportUseExistingFileServicesAndRejectInvalidDocuments()
    {
        var store = new Store();
        var open = Substitute.For<IFileOpenService>();
        var save = Substitute.For<IFileSaveService>();
        string? exported = null;
        save.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            exported = new StreamReader(call.Arg<Stream>()!, leaveOpen: true).ReadToEnd();
            return Task.FromResult<string?>("warnings.json");
        });
        var (page, editor) = Create(store, open, save);
        using (page)
        {
            await page.ActivateAsync();
            editor.SaveCommand.Execute(null);
            await page.ExportCommand.ExecuteAsync(null);
            Assert.Contains("New warning", exported ?? "", StringComparison.Ordinal);
            open.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<OpenedPlanningFile?>(new("warnings.json", new MemoryStream(Encoding.UTF8.GetBytes(exported!)))));
            await page.ImportCommand.ExecuteAsync(null);
            Assert.Single(page.Rules.Rules);
            Assert.Equal(2, store.Writes);
            open.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<OpenedPlanningFile?>(new("bad.json", new MemoryStream(Encoding.UTF8.GetBytes("{}")))));
            await page.ImportCommand.ExecuteAsync(null);
            Assert.Equal(2, store.Writes);
            Assert.Contains("failed", page.StatusMessage);
            await page.DeactivateAsync();
        }
    }

    private static (WarningManagerViewModel Page, WarningRuleEditorViewModel Editor) Create(Store store,
        IFileOpenService? open = null, IFileSaveService? save = null)
    {
        var dispatcher = new InlineDispatcher();
        var events = Substitute.For<IDomainEventHub>();
        var sources = new WarningSources(new TelemetryFieldCatalog());
        var editor = new WarningRuleEditorViewModel(sources, NullLogger<WarningRuleEditorViewModel>.Instance, dispatcher, events);
        var rules = new WarningRuleListViewModel(NullLogger<WarningRuleListViewModel>.Instance, dispatcher, events);
        var status = new WarningStatusViewModel(NullLogger<WarningStatusViewModel>.Instance, dispatcher, events);
        return (new(rules, editor, status, new(store, sources), sources, Substitute.For<IActiveVehicleContext>(),
            TimeProvider.System, new(sources, TimeProvider.System), NullLogger<WarningManagerViewModel>.Instance, dispatcher, events,
            open ?? Substitute.For<IFileOpenService>(), save ?? Substitute.For<IFileSaveService>()), editor);
    }

    private sealed class Store : IWarningRuleStore
    {
        internal int Writes;
        internal bool BlockReads;
        private string? document;
        public async ValueTask<string?> ReadAsync(CancellationToken token)
        {
            if (BlockReads)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return document;
        }
        public ValueTask WriteAsync(string value, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Writes++;
            document = value;
            return ValueTask.CompletedTask;
        }
        public ValueTask QuarantineAsync(string value, CancellationToken token) => ValueTask.CompletedTask;
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
