using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Diagnostics;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Loads every shared destination using real controls and validates repeated activation.</summary>
[Collection("Document rendering")]
public sealed class DiagnosticPanelViewTests
{
    /// <summary>Composes an offline application with explicit diagnostic dependencies.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var active = Substitute.For<IActiveVehicleContext>();
        var id = new VehicleId(16, 1);
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, DateTimeOffset.UtcNow,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        active.VehicleId.Returns(id);
        active.State.Returns(state);
        var diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        diagnostics.Vehicles.Returns(new[] { id });
        diagnostics.GetSnapshot(id).Returns(new VehicleLiveDiagnosticSnapshot(id, state, "Serial", "COM15", false, 1, DateTimeOffset.UtcNow));
        diagnostics.GetArming(id).Returns(new VehicleArmingDiagnostic("Disarmed / readiness unknown", false, null, [], null, null, null));
        var settings = Substitute.For<IPlannerSettingsService>();
        settings.Current.Returns(new PlannerSettings());
        var domain = Substitute.For<IDomainEventHub>();
        var messageStore = Substitute.For<IVehicleMessageStore>();
        var applicationMessages = Substitute.For<IApplicationNotificationStore>();
        var clipboard = Substitute.For<ITextClipboardService>();
        var windows = Substitute.For<IInspectorWindowService>();
        windows.IsSupported.Returns(true);
        var services = new ServiceCollection().AddLogging().AddSingleton(domain).AddSingleton(Substitute.For<IUiDispatcher>())
            .AddSingleton(_ => new MessagesTabViewModel(active, messageStore, applicationMessages, clipboard,
                Substitute.For<IFileSaveService>(), NullLogger<MessagesTabViewModel>.Instance))
            .AddSingleton(_ => new StatusTabViewModel(active, Substitute.For<ITelemetryFieldCatalog>(), Substitute.For<ITelemetrySnapshotProjector>(),
                settings, domain, Substitute.For<IDateTimeProvider>(), NullLogger<StatusTabViewModel>.Instance))
            .AddSingleton(windows)
            .AddSingleton(provider => new LiveTelemetryInspectorViewModel(diagnostics, active, clipboard, windows,
                TimeProvider.System, Substitute.For<IUiDispatcher>(), domain, NullLogger<LiveTelemetryInspectorViewModel>.Instance,
                new PreflightAssessmentService(null, diagnostics), messages: provider.GetRequiredService<MessagesTabViewModel>(),
                status: provider.GetRequiredService<StatusTabViewModel>()))
            .AddSingleton(diagnostics).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services)).UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>Header, all destinations and technical subsections remain usable in a 360px panel.</summary>
    [Fact]
    public async Task DestinationsMountAndRetainSelectionAtNarrowWidth()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DiagnosticPanelViewTests));
        try
        {
            await session.Dispatch(() =>
            {
                var app = (MissionPlanner.App.App)Application.Current!;
                var model = app.ServiceProvider.GetRequiredService<LiveTelemetryInspectorViewModel>();
                var view = new LiveTelemetryInspectorView();
                var window = new Window { Content = view, Width = 360, Height = 900 };
                window.Show();
                foreach (var destination in new[] { "Readiness", "Messages", "Inspector", "Messages", "Readiness", "Inspector" })
                {
                    model.OpenDestinationCommand.Execute(destination);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.Equal(destination, model.Destination);
                    Assert.True(model.IsOpen);
                    Assert.Contains("COM15", model.Header);
                    Assert.DoesNotContain("FirmwareSemanticVersion", model.TechnicalDetails);
                    Assert.InRange(view.Bounds.Width, 1, 360);
                    SaveImage(view, destination);
                    var detach = view.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, model.DetachCommand));
                    var close = view.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, model.CloseCommand));
                    Assert.True(detach.IsEffectivelyVisible);
                    Assert.True(close.IsEffectivelyVisible);
                    Assert.Same(detach.Parent, close.Parent);
                    model.DetachCommand.Execute(null);
                    Dispatcher.UIThread.RunJobs();
                    Assert.False(detach.IsEffectivelyVisible);
                    Assert.False(close.IsEffectivelyVisible);
                    var windowService = app.ServiceProvider.GetRequiredService<IInspectorWindowService>();
                    var closed = (Action)windowService.ReceivedCalls().Last(call => call.GetMethodInfo().Name == "Show").GetArguments()[1]!;
                    closed();
                    model.OpenDestinationCommand.Execute(destination);
                }
                var messages = app.ServiceProvider.GetRequiredService<MessagesTabViewModel>();
                messages.SearchText = "battery";
                model.OpenDestinationCommand.Execute("Readiness");
                model.OpenDestinationCommand.Execute("Messages");
                Assert.Equal("battery", messages.SearchText);
                model.OpenDestinationCommand.Execute("Inspector");
                foreach (var panel in model.Panels)
                {
                    model.SelectedPanel = panel;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.Equal(panel, model.SelectedPanel);
                    Assert.Equal(model.ShowStatus,
                        view.GetVisualDescendants().OfType<StatusTabItemView>().Single().IsEffectivelyVisible);
                    var details = view.FindControl<ListBox>("InspectorDetailsList")!;
                    var marker = view.FindControl<TextBox>("InspectorMarkerText")!;
                    var detailsTop = details.TranslatePoint(default, view)!.Value.Y;
                    var markerTop = marker.TranslatePoint(default, view)!.Value.Y;
                    Assert.True(detailsTop + details.Bounds.Height <= markerTop,
                        $"{panel}: details must stay above the marker field.");
                }
                Assert.Single(view.GetVisualDescendants().OfType<MessagesTabItemView>());
                Assert.Single(view.GetVisualDescendants().OfType<StatusTabItemView>());
                var diagnostics = app.ServiceProvider.GetRequiredService<IVehicleLiveDiagnostics>();
                var id = model.SelectedVehicle!.Value;
                var snapshot = diagnostics.GetSnapshot(id);
                window.Width = 700;
                model.SelectedPanel = "Status";
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                SaveImage(view, "Desktop");
                var connectionDetails = view.GetVisualDescendants().OfType<Expander>()
                    .Single(item => Equals(item.Header, "Vehicle and technical details"));
                connectionDetails.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                SaveImage(view, "Expanded-details");
                var pinned = new VehicleId(17, 1);
                diagnostics.GetSnapshot(pinned).Returns(snapshot with
                {
                    VehicleId = pinned,
                    State = snapshot.State! with { VehicleId = pinned },
                    Endpoint = "OFFLINE TEST DATA — second vehicle"
                });
                var pinnedArming = diagnostics.GetArming(id);
                diagnostics.GetArming(pinned).Returns(pinnedArming);
                diagnostics.Vehicles.Returns(new[] { id, pinned });
                model.Refresh();
                model.OpenEvidence(pinned, "fc-arming");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                SaveImage(view, "Pinned-other-vehicle");
                Assert.Contains("PINNED", model.SelectionSummary);
                model.FollowActiveVehicleCommand.Execute(null);
                connectionDetails.IsExpanded = false;
                window.Width = 360;
                model.FreezeCommand.Execute(null);
                diagnostics.GetSnapshot(id).Returns(snapshot with { Disconnected = true });
                model.Refresh();
                Assert.Contains("DISCONNECTED", model.SelectionSummary);
                Assert.Contains("DISPLAY PAUSED", model.LiveLabel);
                model.OpenDestinationCommand.Execute("Readiness");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                SaveImage(view, "Disconnected-paused");
                window.Close();
                model.Dispose();
                messages.Dispose();
                app.ServiceProvider.GetRequiredService<StatusTabViewModel>().Dispose();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private static void SaveImage(Control view, string scenario)
    {
        var directory = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            var visual = (Visual?)TopLevel.GetTopLevel(view) ?? view;
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize((int)visual.Bounds.Width, (int)visual.Bounds.Height));
            bitmap.Render(visual);
            bitmap.Save(Path.Combine(directory, $"panel-{scenario}.png"));
        }
    }
}
