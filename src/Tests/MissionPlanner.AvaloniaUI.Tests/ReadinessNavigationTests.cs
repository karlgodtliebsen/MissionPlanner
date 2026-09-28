using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Diagnostics;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Shared diagnostic navigation does not alter operational targeting or blocker evidence.</summary>
[Collection("Design preview")]
public sealed class ReadinessNavigationTests
{
    /// <summary>Pinned evidence, badges, navigation and shared battery assessments retain vehicle scope.</summary>
    [Fact]
    public async Task EvidenceNavigationAndReadBadgesDoNotRetargetOrResolve()
    {
        var first = new VehicleId(16, 1);
        var second = new VehicleId(17, 1);
        var active = Substitute.For<IActiveVehicleContext>();
        var state = new VehicleState(first, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
            DateTimeOffset.UtcNow, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        active.VehicleId.Returns(first);
        active.State.Returns(state);
        var diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        diagnostics.Vehicles.Returns(new[] { first, second });
        diagnostics.GetSnapshot(Arg.Any<VehicleId>()).Returns(call => new VehicleLiveDiagnosticSnapshot(call.Arg<VehicleId>(),
            state with { VehicleId = call.Arg<VehicleId>() }, "test", "fixture", false, 1, DateTimeOffset.UtcNow));
        diagnostics.GetArming(Arg.Any<VehicleId>()).Returns(new VehicleArmingDiagnostic("Not ready", false, false, [], null, null, null));
        diagnostics.GetBatteryArmingEvidence(first).Returns(new[] { new BatteryArmingEvidence(1, "PreArm: Battery 1 below minimum", DateTimeOffset.UtcNow) });
        var messages = Substitute.For<IVehicleMessageStore>();
        messages.GetMessages(first).Returns(new[] { new VehicleStatusText(first, 16, 1, MavSeverity.Warning, "Battery 1", DateTimeOffset.UtcNow, Identity: 1) });
        var navigation = Substitute.For<INavigationService>();
        using var model = new LiveTelemetryInspectorViewModel(diagnostics, active, Substitute.For<ITextClipboardService>(),
            Substitute.For<IInspectorWindowService>(), TimeProvider.System, Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(),
            NullLogger<LiveTelemetryInspectorViewModel>.Instance, new PreflightAssessmentService(null, diagnostics), navigation, messages);
        model.OpenDestinationCommand.Execute("Readiness");
        Assert.Equal(1, model.UnreadWarnings);
        var battery = Assert.Single(model.ReadinessChecks, item => item.Key == "battery");
        Assert.Equal("✖ Failed", battery.ResultLabel);
        model.MarkMessagesReadCommand.Execute(null);
        Assert.Equal(0, model.UnreadWarnings);
        Assert.Equal("✖ Failed", battery.ResultLabel);
        battery.EvidenceCommand.Execute(null);
        Assert.Equal("Inspector", model.Destination);
        Assert.Equal("Power", model.SelectedPanel);
        Assert.Contains(model.Details, item => item.Contains("FC blocker"));
        model.OpenEvidence(second, "fc-arming");
        Assert.True(model.IsVehiclePinned);
        Assert.Equal("Messages", model.Destination);
        Assert.Equal(first, active.VehicleId);
        Assert.Contains("PINNED", model.SelectionSummary);
        Assert.Contains(first.ToString(), model.SelectionSummary);
        model.CloseCommand.Execute(null);
        model.OpenDestinationCommand.Execute("Readiness");
        Assert.Equal(second, model.SelectedVehicle);
        model.FollowActiveVehicleCommand.Execute(null);
        Assert.Equal(first, model.SelectedVehicle);
        Assert.False(model.IsVehiclePinned);
        await model.OpenLogsCommand.ExecuteAsync(null);
        await navigation.Received(1).NavigateAsync(MissionPlannerRoutes.Logs);
        Assert.Single(diagnostics.GetBatteryArmingEvidence(first));
    }
}
