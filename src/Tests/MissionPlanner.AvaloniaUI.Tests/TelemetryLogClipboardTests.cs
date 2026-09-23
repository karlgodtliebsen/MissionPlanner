using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Logs;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Logging;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies complete telemetry clipboard exports through the actual packet reader.</summary>
[Collection("Design preview")]
public sealed class TelemetryLogClipboardTests
{
    /// <summary>Copy reads beyond one page and cancellation never submits partial clipboard content.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopiesCompleteSelectedRecording(bool cancel)
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.CheckAccess().Returns(true);
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
        using var services = new ServiceCollection().AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider();
        using var scope = (IDisposable)typeof(AvaloniaLocator)
            .GetMethod("EnterScope", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        var locator = typeof(AvaloniaLocator)
            .GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var binding = locator.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(Application)).Invoke(locator, null)!;
        binding.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(MissionPlanner.App.App))
            .Invoke(binding, [new MissionPlanner.App.App(services)]);

        var storage = new BrowserLogStorage();
        var files = new LogFileOperations(storage);
        using var input = new MemoryStream();
        var timestamp = new byte[8];
        byte[] frame = [0xFD, 0, 0, 0, 0, 1, 2, 0xFD, 0xC0, 0, 0, 0];
        for (var i = 0; i < 600; i++)
        {
            BinaryPrimitives.WriteUInt64BigEndian(timestamp, (ulong)i * 1000000);
            input.Write(timestamp);
            input.Write(frame);
        }
        input.Position = 0;
        await files.ImportAsync(LogStorageArea.Telemetry, "snapshot.tlog", input, TestContext.Current.CancellationToken);
        var catalog = new TelemetryLogCatalog(storage, new TelemetryLogReader(), Substitute.For<IMavLinkMessageDecodeHandler>());
        var log = Assert.Single(await catalog.ListAsync(TestContext.Current.CancellationToken));
        var reader = new TelemetryLogReader();
        var browser = new TelemetryPacketBrowser(reader, Substitute.For<IMavLinkMessageDecodeHandler>(),
            new MavLinkMessageDefinitionRegistry());
        var clipboard = Substitute.For<ITextClipboardService>();
        string? copied = null;
        clipboard.SetTextAsync(Arg.Any<string>()).Returns(call =>
        {
            copied = call.Arg<string>();
            return Task.CompletedTask;
        });
        var dialogs = Substitute.For<IDialogService>();
        var progress = Substitute.For<IDisposable>();
        dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(),
            Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (cancel)
            {
                call.Arg<DialogOptions>()!.RequestCancellation?.Invoke();
            }
            return Task.FromResult(progress);
        });
        using var model = new TelemetryLogsTabViewModel(Substitute.For<IReplaySessionManager>(),
            Substitute.For<IActiveVehicleContext>(), Substitute.For<IFileOpenService>(),
            NullLogger<TelemetryLogsTabViewModel>.Instance, storage, files, browser, reader, catalog,
            Substitute.For<IFileSaveService>(), Substitute.For<ILogFolderService>(), dialogs, clipboard);
        Assert.False(model.CopySnapshotCommand.CanExecute(null));
        log = log with { File = log.File with { Size = 0 }, Metadata = new TelemetryLogMetadata() };
        model.SelectedLog = log;
        model.PacketSearch = "no match";
        Assert.True(model.CopySnapshotCommand.CanExecute(null));
        await model.CopySnapshotCommand.ExecuteAsync(null);
        Assert.False(model.IsBusy);
        Assert.Empty(model.Packets);
        Assert.Same(log, model.SelectedLog);
        progress.Received(1).Dispose();
        if (cancel)
        {
            Assert.Null(copied);
            Assert.Equal("Operation cancelled.", model.StatusMessage);
            return;
        }
        Assert.Null(model.ErrorMessage);
        using var json = JsonDocument.Parse(copied!);
        var recording = json.RootElement.GetProperty("Recording");
        var index = json.RootElement.GetProperty("Index");
        Assert.Equal(index.GetProperty("Length").GetInt64(), recording.GetProperty("Size").GetInt64());
        Assert.Equal(index.GetProperty("Duration").GetString(), recording.GetProperty("Duration").GetString());
        Assert.Equal(600, recording.GetProperty("Metadata").GetProperty("PacketCount").GetInt32());
        var packets = json.RootElement.GetProperty("Packets");
        Assert.Equal(600, packets.GetArrayLength());
        Assert.Equal(599, packets[0].GetProperty("Index").GetInt32());
        Assert.Equal(0, packets[599].GetProperty("Index").GetInt32());
        Assert.Equal(Convert.ToHexString(frame), packets[0].GetProperty("RawHex").GetString());
        Assert.Equal("snapshot.tlog", json.RootElement.GetProperty("Recording").GetProperty("Name").GetString());
    }
}
