using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Test.Support;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies concrete multi-reason arming evidence and command/heartbeat separation.</summary>
public sealed class VehicleArmingDiagnosticTests
{
    /// <summary>Transient reasons expire, last failure persists, and an accepted ACK never means armed.</summary>
    [Fact]
    public async Task ReasonsExpireAndAcceptedAckDoesNotArm()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(hub, domain, clock, Options.Create(new VehicleLiveDiagnosticOptions()));
        var id = new VehicleId(17, 1);
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(VehicleLiveDiagnosticsTests.State(id)), TestContext.Current.CancellationToken);
        foreach (var text in new[] { "PreArm: Battery failsafe", "PreArm: Logging failed", "Arm: throttle too high" })
        {
            await domain.PublishDomainEventAsync(new VehicleStatusTextReceived(new VehicleStatusText(
                id, id.SystemId, id.ComponentId, MavSeverity.Warning, text, clock.GetUtcNow())), TestContext.Current.CancellationToken);
        }
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetArming(id).LastArmFailure is not null);
        var arming = diagnostics.GetArming(id);
        Assert.Equal("DISARMED / NOT READY", arming.Summary);
        Assert.Contains("Battery failsafe", arming.Reasons);
        Assert.Contains("Logging failed", arming.Reasons);
        Assert.Empty(diagnostics.GetArming(new VehicleId(19, 1)).Reasons);

        var transaction = Guid.NewGuid();
        await ((IVehicleTelemetryEventHub)hub).PublishAsync(new VehicleCommandDiagnostic(
            id, clock.GetUtcNow(), 400, transaction, "TX", "Arm", true), TestContext.Current.CancellationToken);
        await ((IVehicleTelemetryEventHub)hub).PublishAsync(new VehicleCommandDiagnostic(
            id, clock.GetUtcNow(), 400, transaction, "ACK", "Accepted", true), TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetArming(id).LastArmResult == "Accepted");
        Assert.False(diagnostics.GetArming(id).IsArmed);
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Empty(diagnostics.GetArming(id).Reasons);
        Assert.Equal("throttle too high", diagnostics.GetArming(id).LastArmFailure);
        Assert.Equal("ARMING UNKNOWN", diagnostics.GetArming(id).Summary);
    }

    /// <summary>Confirmed health establishes readiness; the heartbeat remains authoritative for armed state.</summary>
    [Fact]
    public async Task ReadyAndArmedComeFromVehicleEvidence()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(hub, domain, clock, Options.Create(new VehicleLiveDiagnosticOptions()));
        var id = new VehicleId(23, 1);
        var state = VehicleLiveDiagnosticsTests.State(id);
        state = state with { Health = state.Health with
        {
            SensorsPresent = 1u << 28, SensorsEnabled = 1u << 28, SensorsHealthy = 1u << 28,
            SystemObservedAt = clock.GetUtcNow()
        }};
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetArming(id).IsReadyToArm == true);
        Assert.Equal("DISARMED / READY", diagnostics.GetArming(id).Summary);
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(state with { Flight = state.Flight with { IsArmed = true } }),
            TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetArming(id).IsArmed);
        Assert.Equal("ARMED", diagnostics.GetArming(id).Summary);
    }
}
