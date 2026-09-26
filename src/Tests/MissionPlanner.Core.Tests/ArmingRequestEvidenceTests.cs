using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Test.Support;

namespace MissionPlanner.Core.Tests;

/// <summary>Distinguishes requests, configured gestures, and healthy disarmed vehicles.</summary>
public sealed class ArmingRequestEvidenceTests
{
    /// <summary>Only configured switch movements become inferred request evidence; no ACK is needed for heartbeat truth.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RcEvidenceIsConditionalOnConfiguration(bool configured, bool stick)
    {
        using var f = new Fixture();
        f.Store("RC5_OPTION", configured ? 153 : 0);
        f.Store("ARMING_RUDDER", 2);
        f.Store("RCMAP_THROTTLE", 3);
        f.Store("RCMAP_YAW", 4);
        await f.Publish(f.State);
        Assert.Contains("No recent arm request", f.Diagnostics.GetArming(f.Id).Guidance);
        Assert.Contains("Stick arming: Enabled", f.Diagnostics.GetArming(f.Id).Guidance);
        f.Clock.Advance(TimeSpan.FromMilliseconds(100));
        var state = f.State with { Radio = f.State.Radio with
        {
            ChannelsRaw = new ushort[] { 1500, 1500, 1000, (ushort)(stick ? 2000 : 1500), 2000 },
            ObservedAt = f.Clock.GetUtcNow()
        }};
        await f.Publish(state);
        var arming = f.Diagnostics.GetArming(f.Id);
        Assert.False(arming.IsArmed);
        Assert.Empty(arming.Reasons);
        Assert.Null(arming.LastArmAck);
        if (configured || stick)
        {
            Assert.Equal(ArmingDiagnosticStage.ArmRequested, arming.Stage);
            Assert.Contains("inferred", arming.LastArmCommandSource);
            Assert.Contains("heartbeat confirmation required", arming.LastArmResult);
        }
        else
        {
            Assert.Equal(ArmingDiagnosticStage.DisarmedReady, arming.Stage);
            Assert.Contains("No recent arm request", arming.Guidance);
        }
        await f.Publish(state with { Flight = state.Flight with { IsArmed = true } });
        Assert.Equal(ArmingDiagnosticStage.Armed, f.Diagnostics.GetArming(f.Id).Stage);
        Assert.Null(f.Diagnostics.GetArming(f.Id).LastArmAck);
    }

    /// <summary>Only a matching explicit rejection reports rejected; acceptance waits for heartbeat.</summary>
    [Theory]
    [InlineData((byte)0, ArmingDiagnosticStage.ArmRequested)]
    [InlineData((byte)2, ArmingDiagnosticStage.ArmRejected)]
    public async Task CommandsPreserveAcknowledgementEvidence(byte ack, ArmingDiagnosticStage expected)
    {
        using var f = new Fixture();
        await f.Publish(f.State);
        var correlation = Guid.NewGuid();
        var bus = (IVehicleTelemetryEventHub)f.Hub;
        await bus.PublishAsync(new VehicleCommandDiagnostic(f.Id, f.Clock.GetUtcNow(), 400, correlation, "TX", "Arm", true), TestContext.Current.CancellationToken);
        await bus.PublishAsync(new VehicleCommandDiagnostic(f.Id, f.Clock.GetUtcNow(), 400, correlation, "ACK", "Response", true) { CommandAck = ack }, TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => f.Diagnostics.GetArming(f.Id).LastArmAck == ack);
        Assert.Equal(expected, f.Diagnostics.GetArming(f.Id).Stage);
        Assert.False(f.Diagnostics.GetArming(f.Id).IsArmed);
        Assert.Null(f.Diagnostics.GetArming(f.Id).Guidance);
        await f.Publish(f.State with { Flight = f.State.Flight with { IsArmed = true } });
        correlation = Guid.NewGuid();
        await bus.PublishAsync(new VehicleCommandDiagnostic(f.Id, f.Clock.GetUtcNow(), 400, correlation, "TX", "Disarm"), TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => f.Diagnostics.GetArming(f.Id).Stage == ArmingDiagnosticStage.DisarmRequested);
        await f.Publish(f.State);
        Assert.Equal(ArmingDiagnosticStage.DisarmedReady, f.Diagnostics.GetArming(f.Id).Stage);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly VehicleId Id = new(1, 1);
        internal readonly ManualTimeProvider Clock = new(DateTimeOffset.UtcNow);
        internal readonly EventHub Hub = new(NullLogger<EventHub>.Instance);
        internal readonly DomainEventHub Domain = new(NullLogger<EventHub>.Instance);
        internal readonly VehicleParameterRegistry Parameters = new();
        internal readonly VehicleLiveDiagnostics Diagnostics;
        internal readonly VehicleState State;
        internal Fixture()
        {
            Diagnostics = new(Hub, Domain, Clock, Options.Create(new VehicleLiveDiagnosticOptions()), Parameters);
            var state = VehicleLiveDiagnosticsTests.State(Id);
            State = state with
            {
                Health = state.Health with { SensorsPresent = 1u << 28, SensorsEnabled = 1u << 28, SensorsHealthy = 1u << 28, SystemObservedAt = Clock.GetUtcNow() },
                Radio = state.Radio with { ChannelCount = 5, ChannelsRaw = new ushort[] { 1500, 1500, 1000, 1500, 999 }, ObservedAt = Clock.GetUtcNow(), RssiPercent = 100 }
            };
        }
        internal async Task Publish(VehicleState state)
        {
            var version = Diagnostics.GetSnapshot(Id).Version;
            await Domain.PublishDomainEventAsync(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
            await VehicleLiveDiagnosticsTests.UntilAsync(() => Diagnostics.GetSnapshot(Id).Version > version);
        }
        internal void Store(string name, float value) => Parameters.StoreParameter(Id,
            new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
        public void Dispose() { Diagnostics.Dispose(); Hub.Dispose(); Domain.Dispose(); }
    }
}
