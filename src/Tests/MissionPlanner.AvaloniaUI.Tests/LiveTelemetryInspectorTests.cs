using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Diagnostics;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies session-owned Inspector presentation without native desktop windows.</summary>
[Collection("Design preview")]
public sealed class LiveTelemetryInspectorTests
{
    /// <summary>Freeze affects only presentation, and context never overrides a manual panel choice.</summary>
    [Fact]
    public void FreezeKeepsDisplayWhileDiagnosticsAndMarkersContinue()
    {
        var id = new VehicleId(27, 1);
        var diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        diagnostics.Vehicles.Returns(new[] { id });
        var current = new VehicleLiveDiagnosticSnapshot(id, null, null, null, false, 1, DateTimeOffset.UtcNow);
        diagnostics.GetSnapshot(id).Returns(_ => current);
        var arming = new VehicleArmingDiagnostic("ARMING UNKNOWN", false, null, [], null, null, null);
        diagnostics.GetArming(id).Returns(_ => arming);
        var events = new List<VehicleDiagnosticEvent>();
        diagnostics.GetRecentEvents(id, Arg.Any<int>()).Returns(_ => events.ToArray());
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(id);
        using var model = new LiveTelemetryInspectorViewModel(diagnostics, active, Substitute.For<ITextClipboardService>(), Substitute.For<IInspectorWindowService>(),
            TimeProvider.System, Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(),
            NullLogger<LiveTelemetryInspectorViewModel>.Instance);
        model.SuggestContext("Radio");
        Assert.False(model.IsOpen);
        model.Open();
        model.SuggestContext("Battery");
        Assert.Equal("Power", model.SelectedPanel);
        model.SelectedPanel = "Sensors";
        model.SuggestContext("Motor");
        Assert.Equal("Sensors", model.SelectedPanel);
        model.FreezeCommand.Execute(null);
        var header = model.Header;
        current = current with { Disconnected = true, Version = 2 };
        arming = arming with { Summary = "CONNECTION LOST" };
        events.Add(new(id, DateTimeOffset.UtcNow, "Connection", "Disconnected"));
        model.Refresh();
        Assert.Equal(header, model.Header);
        Assert.Empty(model.Events);
        model.MarkerText = "Pressed Motor 2 test";
        model.AddMarkerCommand.Execute(null);
        diagnostics.Received(1).AddMarker(id, "Pressed Motor 2 test");
        model.FreezeCommand.Execute(null);
        Assert.Contains("CONNECTION LOST", model.Header);
        Assert.Single(model.Events);
        Assert.Contains("stale", model.LiveLabel);
        model.ClearEventsCommand.Execute(null);
        diagnostics.Received(1).ClearEvents(id);
        Assert.Empty(model.Events);
    }

    /// <summary>Copy uses the selected vehicle's complete export even when the presentation is frozen.</summary>
    [Fact]
    public async Task CopyUsesCompleteSnapshot()
    {
        var id = new VehicleId(27, 1);
        var diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        var clipboard = Substitute.For<ITextClipboardService>();
        const string snapshot = "{\"Raw\":[{\"Payload\":\"0102\"}],\"Events\":[]}";
        diagnostics.CreateSnapshotJson(id).Returns(snapshot);
        using var model = new LiveTelemetryInspectorViewModel(diagnostics,
            Substitute.For<IActiveVehicleContext>(), clipboard, Substitute.For<IInspectorWindowService>(),
            TimeProvider.System, Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(),
            NullLogger<LiveTelemetryInspectorViewModel>.Instance);
        await model.CopyEventsCommand.ExecuteAsync(null);
        await clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
        model.SelectedVehicle = id;
        model.FreezeCommand.Execute(null);
        model.RawFilter = "hidden";
        await model.CopyEventsCommand.ExecuteAsync(null);
        diagnostics.Received(1).CreateSnapshotJson(id);
        await clipboard.Received(1).SetTextAsync(snapshot);
    }

    /// <summary>Drawer state, vehicle choice and panel selection survive close and reopen.</summary>
    [Fact]
    public void BrowserDrawerNeedsNoNativeWindowAndRetainsSelection()
    {
        var diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        var first = new VehicleId(7, 1);
        var second = new VehicleId(18, 1);
        diagnostics.Vehicles.Returns(new[] { first, second });
        diagnostics.GetSnapshot(Arg.Any<VehicleId>()).Returns(call =>
            new VehicleLiveDiagnosticSnapshot(call.Arg<VehicleId>(), null, null, null, false, 0, DateTimeOffset.UtcNow));
        diagnostics.GetArming(Arg.Any<VehicleId>()).Returns(new VehicleArmingDiagnostic(
            "ARMING UNKNOWN", false, null, [], null, null, null));
        diagnostics.GetRecentEvents(Arg.Any<VehicleId>(), Arg.Any<int>()).Returns(Array.Empty<VehicleDiagnosticEvent>());
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(first);
        var windows = Substitute.For<IInspectorWindowService>();
        using var model = new LiveTelemetryInspectorViewModel(diagnostics, active, Substitute.For<ITextClipboardService>(), windows, TimeProvider.System,
            Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(), NullLogger<LiveTelemetryInspectorViewModel>.Instance);
        model.Open();
        Assert.True(model.IsDrawerOpen);
        Assert.False(model.CanDetach);
        Assert.Equal(first, model.SelectedVehicle);
        model.SelectedVehicle = second;
        model.SelectedPanel = "Power";
        model.DrawerWidth = 650;
        model.DetachCommand.Execute(null);
        windows.DidNotReceive().Show(Arg.Any<LiveTelemetryInspectorViewModel>(), Arg.Any<Action>());
        model.CloseCommand.Execute(null);
        Assert.False(model.IsDrawerOpen);
        model.Open();
        Assert.Equal(second, model.SelectedVehicle);
        Assert.Equal("Power", model.SelectedPanel);
        Assert.Equal(650, model.DrawerWidth);
    }
}
