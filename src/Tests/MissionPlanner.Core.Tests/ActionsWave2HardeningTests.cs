using FluentAssertions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Cross-cutting Wave 2 isolation and lifecycle regressions.</summary>
public sealed class ActionsWave2HardeningTests
{
    [Fact]
    public async Task SameCommandAcksLocalReferencesAndOperationLeasesRemainVehicleIsolated()
    {
        var vehicleA = new VehicleId(1, 1);
        var vehicleB = new VehicleId(2, 1);
        var tracker = new CommandAckTracker();
        var waitA = tracker.WaitForAckAsync(vehicleA, 224, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var waitB = tracker.WaitForAckAsync(vehicleB, 224, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        tracker.Handle(new CommandAckMessage(1, 1, new TransportEndPoint("A"), 224, 0, DateTimeOffset.UtcNow));

        (await waitA).SystemId.Should().Be(1);
        waitB.IsCompleted.Should().BeFalse();

        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleRegistered, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleDisconnected, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());
        var references = new LocalAltitudeReferenceService(eventHub);
        references.TryZero(vehicleA, 37.5).Should().BeTrue();
        references.HasReference(vehicleA).Should().BeTrue();
        references.HasReference(vehicleB).Should().BeFalse();

        var gate = new VehicleOperationGate();
        gate.TryAcquire(vehicleA, "A mission operation", out var leaseA).Should().BeTrue();
        gate.TryAcquire(vehicleB, "B adjustment", out var leaseB).Should().BeTrue();
        using (leaseA)
        using (leaseB)
        {
            gate.GetCurrentOperation(vehicleA).Should().Be("A mission operation");
            gate.GetCurrentOperation(vehicleB).Should().Be("B adjustment");
        }

        tracker.Handle(new CommandAckMessage(2, 1, new TransportEndPoint("B"), 224, 0, DateTimeOffset.UtcNow));
        (await waitB).SystemId.Should().Be(2);
    }
}
