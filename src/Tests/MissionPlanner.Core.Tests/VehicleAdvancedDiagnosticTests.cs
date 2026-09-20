using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks bounded Raw retention and conservative motor/sensor explanations.</summary>
public sealed class VehicleAdvancedDiagnosticTests
{
    /// <summary>The real bounded inspection tap delivers unknown frames without affecting the domain pipeline.</summary>
    [Fact]
    public async Task RawAdapterUsesConnectionLeaseAndReleasesOnDisconnect()
    {
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(hub, domain, TimeProvider.System, Options.Create(new VehicleLiveDiagnosticOptions()));
        var tap = new MissionPlanner.MavLink.Services.MavLinkInspectionTap();
        var protocol = NSubstitute.Substitute.For<MissionPlanner.MavLink.Services.Abstractions.IMavLinkConnection>();
        NSubstitute.SubstituteExtensions.Returns(protocol.Inspection, tap);
        var session = NSubstitute.Substitute.For<MissionPlanner.Core.Vehicles.Abstractions.IVehicleConnectionSession>();
        NSubstitute.SubstituteExtensions.Returns(session.Connection, protocol);
        using var source = new VehicleRawDiagnosticsSource(domain, hub, session,
            new MissionPlanner.MavLink.Services.MavLinkMessageDefinitionRegistry(), NullLogger<VehicleRawDiagnosticsSource>.Instance);
        var id = new VehicleId(29, 1);
        await domain.PublishDomainEventAsync(new VehicleConnected(id, "UDP", "test", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        Assert.True(tap.HasObservers);
        var frame = new MissionPlanner.MavLink.MavLinkFrame(29, 3, new MissionPlanner.Transport.TransportEndPoint("test"),
            999999, 0, new byte[] { 10, 11 }, new byte[] { 10, 11 }, DateTimeOffset.UtcNow);
        tap.Publish(new(MissionPlanner.MavLink.Services.MavLinkTrafficDirection.Inbound, frame, null, false));
        await VehicleLiveDiagnosticsTests.UntilAsync(() => diagnostics.GetRaw(id).Count == 1);
        Assert.Equal("Unknown", diagnostics.GetRaw(id)[0].Name);
        Assert.Equal("0A0B", diagnostics.GetRaw(id)[0].Payload);
        await domain.PublishDomainEventAsync(new VehicleDisconnected(id, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        Assert.False(tap.HasObservers);
    }

    /// <summary>Unknown messages are retained independently of the significant-event journal.</summary>
    [Fact]
    public async Task RawIsBoundedAndDoesNotFloodJournal()
    {
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var service = new VehicleLiveDiagnostics(hub, domain, TimeProvider.System,
            Options.Create(new VehicleLiveDiagnosticOptions { RawCapacity = 10 }));
        var id = new VehicleId(11, 1);
        for (uint index = 0; index < 100; index++)
        {
            await ((IVehicleTelemetryEventHub)hub).PublishAsync(new VehicleRawDiagnostic(
                id, DateTimeOffset.UtcNow, 11, 2, index, "Unknown", "RX", "00FF"), TestContext.Current.CancellationToken);
        }
        await VehicleLiveDiagnosticsTests.UntilAsync(() => service.GetRaw(id).FirstOrDefault()?.MessageId == 99);
        Assert.Equal(10, service.GetRaw(id).Count);
        Assert.Empty(service.GetRecentEvents(id));
        Assert.Single(service.GetRaw(id, "99", 11, 2));
        Assert.Empty(service.GetRaw(id, component: 1));
        Assert.Empty(service.GetRaw(new VehicleId(12, 1)));
    }

    /// <summary>Output change and accepted motor command cannot prove physical movement.</summary>
    [Fact]
    public async Task OutputChangeDoesNotClaimPhysicalMovement()
    {
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var service = new VehicleLiveDiagnostics(hub, domain, TimeProvider.System, Options.Create(new VehicleLiveDiagnosticOptions()));
        var id = new VehicleId(13, 1);
        var state = VehicleLiveDiagnosticsTests.State(id);
        state = state with { Radio = state.Radio with { ServoOutputsRaw = new ushort[] { 1000, 1200 } } };
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
        await VehicleLiveDiagnosticsTests.UntilAsync(() => service.GetSnapshot(id).State is not null);
        Assert.Contains(service.GetOutputs(id), text => text.Contains("1200"));
        Assert.Contains(service.GetOutputs(id), text => text.Contains("Physical motor movement remains unknown"));
        var sensors = VehicleDiagnosticPanels.Sensors(state with { Health = state.Health with
        {
            SensorsPresent = 4, SensorsEnabled = 0, SensorsHealthy = 0
        }}, DateTimeOffset.UtcNow);
        Assert.Contains(sensors, text => text.Contains("Compass") && text.Contains("disabled / optional"));
    }
}
