using System.Reflection;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies shared parameter progress presentation and dialog lifetime.</summary>
[Collection("Design preview")]
public sealed class ParameterProgressDialogTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task QuickEditorAppliesOnlyEnteredParametersAfterConfirmation(bool accepted, bool detached)
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        await fixture.Model.ActivateAsync();
        fixture.ConfigureWrite(false);
        fixture.Model.DialogHostId = detached ? "quick-editor-window" : null;
        fixture.Dialogs.ConfirmAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>(), "quick-editor-window").Returns(accepted);
        fixture.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(accepted);
        fixture.Session.TrySetPending("TEST_PARAM", 2, out Arg.Any<string?>()).Returns(true);
        var editor = new ParametersEditorViewModel(fixture.Dialogs) { Text = "//Pavo 20 Pro\nTEST_PARAM = 2 // test\nUNKNOWN = 3\nTEST_PARAM = invalid" };
        fixture.DomainFactory.Create<ParametersEditorViewModel>().Returns(editor);
        fixture.Dialogs.ShowStandardAsync<ParametersEditorView, ParametersEditorViewModel>(editor,
            Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                Assert.True(editor.ApplyModifiedCommand.CanExecute(null));
                await editor.ApplyModifiedCommand.ExecuteAsync(null);
                return null;
            });

        if (detached)
        {
            await fixture.Model.OpenQuickEditorAsync(async (presentedEditor, _) =>
            {
                Assert.Same(editor, presentedEditor);
                Assert.Equal("quick-editor-window", editor.DialogHostId);
                Assert.True(editor.ApplyModifiedCommand.CanExecute(null));
                await editor.ApplyModifiedCommand.ExecuteAsync(null);
                return null;
            }, TestContext.Current.CancellationToken);
        }
        else
        {
            await fixture.Model.UseQuickEditorCommand.ExecuteAsync(null);
        }

        fixture.Session.Received(1).TrySetPending("TEST_PARAM", 2, out Arg.Any<string?>());
        fixture.Session.Received(1).CreateWritePlan(Arg.Is<IReadOnlyList<string>>(names => names != null && names.Count == 1 && names[0] == "TEST_PARAM"));
        Assert.Equal(accepted ? 1 : 0, fixture.Session.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "ApplyAsync"));
        Assert.Contains(accepted ? "Confirmed" : "cancelled", editor.StatusMessage);
        Assert.Contains("unknown parameter: UNKNOWN", editor.FeedbackReport);
        Assert.Contains("invalid numeric value", editor.FeedbackReport);
        Assert.Contains("skipped 2 input line(s)", editor.FeedbackReport);
        if (detached)
        {
            await fixture.Dialogs.Received(1).ConfirmAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>(), "quick-editor-window");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("parameters-editor-window-1")]
    [InlineData("parameters-editor-window-2")]
    public async Task QuickEditorUsesOwningDialogHost(string? hostId)
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        await fixture.Model.ActivateAsync();
        fixture.Model.DialogHostId = hostId;
        var editor = new ParametersEditorViewModel(fixture.Dialogs);
        fixture.DomainFactory.Create<ParametersEditorViewModel>().Returns(editor);

        await fixture.Model.UseQuickEditorCommand.ExecuteAsync(null);

        await fixture.Dialogs.Received(1).ShowStandardAsync<ParametersEditorView, ParametersEditorViewModel>(
            editor, Arg.Any<Ursa.Controls.OverlayDialogOptions>(), hostId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("TEST_PARAM = 99\nSECOND = 88")]
    [InlineData("// comment\r\nTEST_PARAM =\r\nSECOND = invalid // comment\r\nUNKNOWN = 3\r\nTEST_PARAM = 4")]
    [InlineData("test_param:99\rSECOND;88")]
    public void QuickEditorPreviewsConfirmedValuesForEnteredNames(string text)
    {
        using var fixture = new Fixture();
        using var editor = new ParametersEditorViewModel(fixture.Dialogs);
        editor.SetCurrentParameters([new("TEST_PARAM", 1.5), new("SECOND", 2)]);
        editor.Text = text;
        Assert.Equal($"TEST_PARAM = 1.5{Environment.NewLine}SECOND = 2", editor.UsedParametersText);
        Assert.Equal(text, editor.Text);

        editor.SetCurrentParameters([new("TEST_PARAM", 3), new("SECOND", 2)]);
        Assert.StartsWith("TEST_PARAM = 3", editor.UsedParametersText);
        editor.ClearCommand.Execute(null);
        Assert.Empty(editor.UsedParametersText);
    }

    [Fact]
    public void QuickEditorSearchAddsCanonicalNameAndPreservesExistingEdits()
    {
        using var fixture = new Fixture();
        using var editor = new ParametersEditorViewModel(fixture.Dialogs);
        editor.SetCurrentParameters([new("TEST_PARAM", 1.5), new("SECOND", 2)]);
        Assert.Equal(new[] { "SECOND", "TEST_PARAM" }, editor.ParameterNames);
        editor.SearchText = "unknown";
        Assert.False(editor.AddParameterCommand.CanExecute(null));
        editor.SearchText = "test_param";
        Assert.True(editor.AddParameterCommand.CanExecute(null));
        editor.AddParameterCommand.Execute(null);
        Assert.Equal($"TEST_PARAM = 1.5{Environment.NewLine}", editor.Text);
        Assert.Equal("TEST_PARAM = 1.5", editor.UsedParametersText);
        Assert.Empty(editor.SearchText);

        editor.Text = "TEST_PARAM = 99";
        editor.SearchText = "TEST_PARAM";
        editor.AddParameterCommand.Execute(null);
        Assert.Equal("TEST_PARAM = 99", editor.Text);
        editor.SearchText = "SECOND";
        editor.AddParameterCommand.Execute(null);
        Assert.Equal($"TEST_PARAM = 99{Environment.NewLine}SECOND = 2{Environment.NewLine}", editor.Text);
        editor.SearchText = "SECOND";
        editor.ClearSearchCommand.Execute(null);
        Assert.Empty(editor.SearchText);
        editor.SetCurrentParameters([]);
        Assert.Empty(editor.UsedParametersText);
        Assert.Empty(editor.ParameterNames);
    }

    [Fact]
    public async Task QuickEditorTracksLiveValuesRatherThanPendingEdits()
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        await fixture.Model.ActivateAsync();
        var field = new ParameterEditField("TEST_PARAM", MavParamType.Real32, 1, 1, 99, ParameterFieldMetadata.Empty, null);
        fixture.Session.Fields.Returns(new[] { field });
        using var editor = new ParametersEditorViewModel(fixture.Dialogs) { Text = "TEST_PARAM = 10" };
        fixture.DomainFactory.Create<ParametersEditorViewModel>().Returns(editor);

        await fixture.Model.OpenQuickEditorAsync((presented, _) =>
        {
            Assert.Equal("TEST_PARAM = 1", presented.UsedParametersText);
            fixture.Session.Fields.Returns(new[] { field with { LiveValue = 2 } });
            fixture.Session.Changed?.Invoke();
            Assert.Equal("TEST_PARAM = 2", presented.UsedParametersText);
            fixture.Session.IsValid.Returns(false);
            fixture.Session.Changed?.Invoke();
            Assert.Empty(presented.UsedParametersText);
            Assert.Empty(presented.ParameterNames);
            Assert.Equal("TEST_PARAM = 10", presented.Text);
            return Task.FromResult<ParametersEditorViewModel?>(null);
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("TEST_PARAM = invalid")]
    [InlineData("TEST_PARAM = NaN")]
    [InlineData("TEST_PARAM = 2\nUNKNOWN = 3")]
    [InlineData("TEST_PARAM = 2 = 3")]
    public void QuickEditorReportsInvalidInputWithoutChangingSource(string text)
    {
        using var fixture = new Fixture();
        var editor = new ParametersEditorViewModel(fixture.Dialogs) { Text = text };
        var original = new VehicleParameter("TEST_PARAM", 1, MavParamType.Real32, 0, 1);
        var source = new List<VehicleParameter> { original };
        var parsed = editor.UpdateParameters(source);
        Assert.Equal(text.Contains("UNKNOWN") ? 1 : 0, parsed.Count);
        Assert.Contains("skipped 1 input line(s)", editor.FeedbackReport);
        Assert.Same(original, Assert.Single(source));
        Assert.Empty(fixture.Session.ReceivedCalls());
    }

    /// <summary>Background progress updates one dialog and terminal states close it.</summary>
    [Theory]
    [InlineData(ParameterLoadState.Completed)]
    [InlineData(ParameterLoadState.Failed)]
    [InlineData(ParameterLoadState.Cancelled)]
    public async Task BackgroundDownloadUsesOneDialogUntilTerminalStatus(ParameterLoadState terminal)
    {
        using var fixture = new Fixture();
        fixture.SetStatus(ParameterLoadState.Starting, "Starting download");
        await fixture.Model.ActivateAsync();

        Assert.True(fixture.Model.IsShowingProgressDialog);
        Assert.Equal("Loading parameters", Assert.Single(fixture.Options).Title);
        Assert.Equal("Starting download", fixture.Message!());
        await fixture.PublishAsync(ParameterLoadState.Downloading, "50 / 100");
        Assert.Single(fixture.Handles);
        Assert.Equal("50 / 100", fixture.Message!());

        await fixture.PublishAsync(terminal, "Finished");
        Assert.False(fixture.Model.IsShowingProgressDialog);
        fixture.Handles[0].Received(1).Dispose();
        Assert.Empty(fixture.Stream.ReceivedCalls());
    }

    /// <summary>The dialog covers metadata projection after the connection download finishes.</summary>
    [Fact]
    public async Task BackgroundDialogStaysOpenUntilMetadataIsReady()
    {
        using var fixture = new Fixture();
        fixture.SetStatus(ParameterLoadState.Downloading, "Downloading");
        await fixture.Model.ActivateAsync();
        fixture.SetCompleteCache();
        var metadata = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.LoadAsync(null, Arg.Any<CancellationToken>()).Returns(metadata.Task);

        var completion = fixture.PublishAsync(ParameterLoadState.Completed, "Downloaded");
        Assert.False(completion.IsCompleted);
        Assert.Equal("Loading parameter metadata...", fixture.Message!());
        fixture.Handles[0].DidNotReceive().Dispose();

        metadata.SetResult();
        await completion;
        Assert.Single(fixture.Model.Parameters);
        fixture.Handles[0].Received(1).Dispose();
        Assert.False(fixture.Model.IsShowingProgressDialog);
        Assert.Single(fixture.Handles);
    }

    /// <summary>Opening a page with a completed cache loads it once under the same dialog.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("quick-editor-window")]
    public async Task CompletedCacheIsProjectedOnceOnActivation(string? hostId)
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        fixture.SetStatus(ParameterLoadState.Completed, "Downloaded");
        fixture.Model.DialogHostId = hostId;

        await fixture.Model.ActivateAsync();

        Assert.Single(fixture.Model.Parameters);
        Assert.Single(fixture.Handles);
        Assert.Equal(hostId, Assert.Single(fixture.Options).HostId);
        await fixture.Session.Received(1).LoadAsync(null, Arg.Any<CancellationToken>());
        fixture.Handles[0].Received(1).Dispose();
        Assert.False(fixture.Model.IsShowingProgressDialog);
    }

    /// <summary>Metadata failures close the dialog and retain an actionable error.</summary>
    [Fact]
    public async Task MetadataFailureClosesDialog()
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        fixture.SetStatus(ParameterLoadState.Completed, "Downloaded");
        fixture.Session.LoadAsync(null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Metadata unavailable")));

        await fixture.Model.ActivateAsync();

        Assert.False(fixture.Model.IsShowingProgressDialog);
        Assert.Single(fixture.Handles).Received(1).Dispose();
        Assert.Contains("Metadata unavailable", fixture.Model.ErrorMessage);
    }

    /// <summary>A late dialog result cannot replace a newer activation's dialog.</summary>
    [Fact]
    public async Task LateDialogIsDisposedAfterPageReactivation()
    {
        using var fixture = new Fixture();
        fixture.SetStatus(ParameterLoadState.Downloading, "Downloading");
        var oldHandle = Substitute.For<IDisposable>();
        var pending = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.OpenDialog = () => pending.Task;
        var activation = fixture.Model.ActivateAsync();
        Assert.False(activation.IsCompleted);

        await fixture.Model.DeactivateAsync();
        fixture.OpenDialog = null;
        await fixture.Model.ActivateAsync();
        var currentHandle = fixture.Handles.Last();

        pending.SetResult(oldHandle);
        await activation;
        oldHandle.Received(1).Dispose();
        currentHandle.DidNotReceive().Dispose();
        Assert.True(fixture.Model.IsShowingProgressDialog);

        await fixture.Model.DeactivateAsync();
        currentHandle.Received(1).Dispose();
        await fixture.PublishAsync(ParameterLoadState.Downloading, "Late update");
        Assert.False(fixture.Model.IsShowingProgressDialog);
        Assert.Equal(2, fixture.Options.Count);
    }

    /// <summary>Background notifications cannot duplicate or close a manual Refresh dialog.</summary>
    [Fact]
    public async Task RefreshRetainsItsDialogUntilItsOwnOperationFinishes()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        var download = new TaskCompletionSource<ParameterStreamResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Stream.StreamAllParametersWithRetryAsync(fixture.VehicleId,
            Arg.Any<IProgress<ParameterStreamProgress>>(), 3, null, Arg.Any<CancellationToken>())
            .Returns(download.Task);

        var refresh = fixture.Model.RefreshParametersCommand.ExecuteAsync(null);
        Assert.False(refresh.IsCompleted);
        Assert.Equal("Loading parameters", Assert.Single(fixture.Options).Title);

        await fixture.PublishAsync(ParameterLoadState.Downloading, "Downloading");
        await fixture.PublishAsync(ParameterLoadState.Completed, "Downloaded");
        Assert.Single(fixture.Handles);
        fixture.Handles[0].DidNotReceive().Dispose();

        download.SetResult(ParameterStreamResult.CreateFailure("Test download failed", TimeSpan.Zero));
        await refresh;
        fixture.Handles[0].Received(1).Dispose();
        Assert.False(fixture.Model.IsShowingProgressDialog);
        Assert.Contains("Test download failed", fixture.Model.ErrorMessage);
    }

    /// <summary>Only confirmed reboot-required changes reconnect, retaining the reboot indication.</summary>
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "quick-editor-window")]
    public async Task ApplyReconnectsOnlyWhenReportRequiresReboot(bool rebootRequired, string? hostId)
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        await fixture.Model.ActivateAsync();
        fixture.ConfigureWrite(rebootRequired);
        fixture.Model.DialogHostId = hostId;
        fixture.Dialogs.ConfirmAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>(), hostId).Returns(true);
        var target = new MissionPlanner.Core.Vehicles.VehicleReconnectTarget(Guid.NewGuid(), "Serial", "COM11", 0, 57600);
        fixture.Connections.CaptureReconnectTarget().Returns(target);
        fixture.Connections.ReconnectAsync(target, Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Assert.Contains("disconnect", fixture.Message!());
                Assert.False(call.Arg<CancellationToken>().IsCancellationRequested);
                return new MissionPlanner.Core.Vehicles.VehicleConnectionResult(true, fixture.VehicleId, null);
            });
        var before = fixture.Handles.Count;
        await fixture.Model.WriteParametersCommand.ExecuteAsync(null);
        Assert.Equal(rebootRequired, fixture.Model.RebootRequired);
        Assert.Equal(rebootRequired ? 1 : 0, fixture.Connections.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "ReconnectAsync"));
        Assert.Equal(before + (rebootRequired ? 1 : 0), fixture.Handles.Count);
        if (rebootRequired)
        {
            fixture.Handles[^1].Received(1).Dispose();
            Assert.Contains("Reconnected", fixture.Model.StatusMessage);
            Assert.Equal(hostId, fixture.Options[^1].HostId);
        }
    }

    /// <summary>Closing the reconnect overlay prevents transport activity and retains confirmed changes.</summary>
    [Fact]
    public async Task CancelReconnectOverlayRetainsRebootRequirement()
    {
        using var fixture = new Fixture();
        fixture.SetCompleteCache();
        await fixture.Model.ActivateAsync();
        fixture.ConfigureWrite(true);
        fixture.Connections.CaptureReconnectTarget().Returns(
            new MissionPlanner.Core.Vehicles.VehicleReconnectTarget(Guid.NewGuid(), "Serial", "COM11", 0, 57600));
        fixture.OpenDialog = () =>
        {
            fixture.Options[^1].RequestCancellation!();
            return Task.FromResult(fixture.Handles[^1]);
        };
        await fixture.Model.WriteParametersCommand.ExecuteAsync(null);
        Assert.True(fixture.Model.RebootRequired);
        Assert.Contains("cancelled", fixture.Model.ErrorMessage);
        Assert.DoesNotContain(fixture.Connections.ReceivedCalls(), call => call.GetMethodInfo().Name == "ReconnectAsync");
        fixture.Handles[^1].Received(1).Dispose();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider services;
        private readonly IDisposable locatorScope;
        private readonly IVehicleParameterLoadStatusContext statusContext = Substitute.For<IVehicleParameterLoadStatusContext>();
        private readonly IVehicleParameterRegistry registry = Substitute.For<IVehicleParameterRegistry>();
        private Func<VehicleParameterLoadStatusChanged, CancellationToken, Task> statusHandler = null!;
        private ParameterLoadStatus? latest;

        internal readonly VehicleId VehicleId = new(1, 1);
        internal readonly IVehicleConnectionService Connections = Substitute.For<IVehicleConnectionService>();
        internal readonly IDialogService Dialogs = Substitute.For<IDialogService>();
        internal readonly IDomainFactory DomainFactory = Substitute.For<IDomainFactory>();
        internal readonly IUserConfirmationService Confirmation = Substitute.For<IUserConfirmationService>();
        internal readonly IParameterEditSession Session = Substitute.For<IParameterEditSession>();
        internal readonly IVehicleParameterStreamService Stream = Substitute.For<IVehicleParameterStreamService>();
        internal readonly List<IDisposable> Handles = [];
        internal readonly List<DialogOptions> Options = [];
        internal Func<string>? Message;
        internal Func<Task<IDisposable>>? OpenDialog;
        internal ParametersEditorTabViewModel Model
        {
            get;
        }

        internal Fixture()
        {
            var dispatcher = new InlineDispatcher();
            var events = Substitute.For<IDomainEventHub>();
            events.SubscribeDomainEventAsync(Arg.Any<Func<VehicleParameterLoadStatusChanged, CancellationToken, Task>>())
                .Returns(call =>
                {
                    statusHandler = call.Arg<Func<VehicleParameterLoadStatusChanged, CancellationToken, Task>>()!;
                    return Substitute.For<IDisposable>();
                });
            services = new ServiceCollection().AddLogging().AddSingleton<IUiDispatcher>(dispatcher)
                .AddSingleton(events).BuildServiceProvider();
            // Use the same isolated locator scope as the parameter-threading tests.
            locatorScope = (IDisposable)typeof(AvaloniaLocator)
                .GetMethod("EnterScope", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
            var locator = typeof(AvaloniaLocator)
                .GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var binding = locator.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(Application)).Invoke(locator, null)!;
            binding.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(MissionPlanner.App.App))
                .Invoke(binding, [new MissionPlanner.App.App(services)]);

            var active = Substitute.For<IActiveVehicleContext>();
            active.VehicleId.Returns(VehicleId);
            active.IsOnline.Returns(true);
            var connection = Substitute.For<IVehicleConnectionSession>();
            connection.ParameterRegistry.Returns(registry);
            connection.ParameterStreamService.Returns(Stream);
            statusContext.Get(VehicleId).Returns(_ => latest);
            var factory = Substitute.For<IParameterEditSessionFactory>();
            factory.Create(VehicleId).Returns(Session);
            Session.IsValid.Returns(true);
            Session.Fields.Returns(new[]
            {
                new ParameterEditField("TEST_PARAM", MavParamType.Real32, 1, 1, 1, ParameterFieldMetadata.Empty, null)
            });
            var dialogs = Dialogs;
            dialogs.CreateOptions(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
                .Returns(_ => new Ursa.Controls.OverlayDialogOptions());
            dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    Message = call.Arg<Func<string>>();
                    Options.Add(call.Arg<DialogOptions>()!);
                    var handle = Substitute.For<IDisposable>();
                    Handles.Add(handle);
                    return OpenDialog?.Invoke() ?? Task.FromResult(handle);
                });
            Model = new
                ParametersEditorTabViewModel(
                    dialogs,
                    DomainFactory,
                    events,
                    connection,
                    active,
                    factory,
                    Substitute.For<ITextClipboardService>(),
                    new ParametersFileHandler(Substitute.For<IFileOpenService>(), Substitute.For<IFileSaveService>()),
                    Confirmation,
                    Substitute.For<IParameterProfileRepository>(),
                    Substitute.For<IParameterProfileService>(),
                    statusContext,
                    NullLogger<ParametersEditorTabViewModel>.Instance,
                    Connections);


        }

        internal void ConfigureWrite(bool rebootRequired)
        {
            Session.IsDirty.Returns(true);
            Session.CreateWritePlan(Arg.Any<IReadOnlyList<string>?>()).Returns(new ParameterWritePlan(
                new ParameterEditScope(VehicleId, null!), DateTimeOffset.UtcNow,
                [new ParameterWritePlanEntry("TEST_PARAM", "Test", 1, 2, null, 1, rebootRequired, false, null)]));
            Session.ApplyAsync(Arg.Any<ParameterWritePlan>(), Arg.Any<IProgress<ParameterApplyProgress>>(), Arg.Any<CancellationToken>())
                .Returns(new ParameterApplyReport(true,
                    [new ParameterWriteResult("TEST_PARAM", ParameterWriteOutcome.Confirmed, "Confirmed")], rebootRequired));
            Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        internal void SetCompleteCache()
        {
            registry.GetParameterCount(VehicleId).Returns((ushort)1);
            registry.GetAllParameters(VehicleId).Returns(new Dictionary<string, VehicleParameter>
            {
                ["TEST_PARAM"] = new("TEST_PARAM", 1, MavParamType.Real32, 0, 1)
            });
        }

        internal void SetStatus(ParameterLoadState state, string message)
        {
            latest = new ParameterLoadStatus(VehicleId, state, 0, 100, 0, message, DateTimeOffset.UtcNow);
        }

        internal Task PublishAsync(ParameterLoadState state, string message)
        {
            SetStatus(state, message);
            return statusHandler(new VehicleParameterLoadStatusChanged(latest!), CancellationToken.None);
        }

        public void Dispose()
        {
            Model.Dispose();
            locatorScope.Dispose();
            services.Dispose();
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            return true;
        }

        public void Dispatch(Action action)
        {
            action();
        }

        public T Dispatch<T>(Func<T> action)
        {
            return action();
        }

        public Task DispatchAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task DispatchAsync(Func<Task> action)
        {
            return action();
        }

        public Task<T> DispatchAsync<T>(Func<T> action)
        {
            return Task.FromResult(action());
        }

        public Task<T> DispatchAsync<T>(Func<Task<T>> action)
        {
            return action();
        }
    }
}
