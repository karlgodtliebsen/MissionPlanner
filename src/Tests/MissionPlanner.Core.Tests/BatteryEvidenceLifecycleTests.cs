using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Battery FC evidence stays scoped to its vehicle and connection session.</summary>
public sealed class BatteryEvidenceLifecycleTests
{
    /// <summary>Only newer positive FC evidence resolves blockers; reconnect clears session scope.</summary>
    [Theory]
    [InlineData("PreArm:", true)]
    [InlineData("Arm:", true)]
    [InlineData("PreArm:", false)]
    [InlineData("Arm:", false)]
    public async Task IsolatesVehiclesResolvesAndResetsOnReconnect(string prefix, bool disconnected)
    {
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(hub, domain, TimeProvider.System,
            Options.Create(new VehicleLiveDiagnosticOptions()));
        var first = new VehicleId(16, 1);
        var second = new VehicleId(17, 1);
        var now = DateTimeOffset.UtcNow;
        var token = TestContext.Current.CancellationToken;
        await domain.PublishDomainEventAsync(new VehicleConnected(first, "test", "fixture", now.AddSeconds(-5)), token);
        await domain.PublishDomainEventAsync(new VehicleStatusTextReceived(new VehicleStatusText(first, 16, 1,
            MavSeverity.Warning, $"{prefix} Battery 1 below minimum arming voltage", now.AddSeconds(-2))), token);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetBatteryArmingEvidence(first).Count == 1);
        Assert.Equal(now.AddSeconds(-5), diagnostics.GetSnapshot(first).SessionStartedAt);
        Assert.Empty(diagnostics.GetBatteryArmingEvidence(second));
        var state = VehicleLiveDiagnosticsTests.State(first);
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(state), token);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetSnapshot(first).State == state);
        Assert.Null(Assert.Single(diagnostics.GetBatteryArmingEvidence(first)).ResolvedAt);
        const uint preArm = 1u << 28;
        var recovered = state with { Health = state.Health with
        {
            SensorsPresent = preArm, SensorsEnabled = preArm, SensorsHealthy = preArm, SystemObservedAt = now
        } };
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(recovered), token);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetBatteryArmingEvidence(first)[0].ResolvedAt is not null);
        Assert.Equal(now, diagnostics.GetBatteryArmingEvidence(first)[0].ResolvedAt);
        if (disconnected)
        {
            await domain.PublishDomainEventAsync(new VehicleDisconnected(first, now, "fixture"), token);
        }
        await domain.PublishDomainEventAsync(new VehicleConnected(first, "test", "new session", now.AddMilliseconds(1)), token);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetSnapshot(first).Endpoint == "new session");
        Assert.Equal(now.AddMilliseconds(1), diagnostics.GetSnapshot(first).SessionStartedAt);
        Assert.Empty(diagnostics.GetBatteryArmingEvidence(first));
        Assert.Contains(diagnostics.GetRecentEvents(first), item => item.Message.Contains("Battery 1"));
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(state with
        {
            Connection = state.Connection with { LastHeartbeatAt = now.AddSeconds(-1) }
        }), token);
        // Late delivery of a message from the previous session remains history, not a new blocker.
        await domain.PublishDomainEventAsync(new VehicleStatusTextReceived(new VehicleStatusText(first, 16, 1,
            MavSeverity.Warning, "PreArm: Battery 2 old session", now)), token);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetRecentEvents(first).Any(item => item.Message.Contains("old session")));
        Assert.Empty(diagnostics.GetBatteryArmingEvidence(first));
        Assert.Null(diagnostics.GetSnapshot(first).State);
        Assert.Empty(diagnostics.GetArming(first).Reasons);
    }
}
