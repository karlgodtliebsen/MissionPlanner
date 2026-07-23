using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Helpers;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies deterministic multi-instance allocation, routing, lifecycle isolation, and recovery.</summary>
[Trait("TestTier", "FakeRuntime")]
public sealed class SimulationFleetTests
{
    /// <summary>Verifies identities, ports, offsets, and artifact paths are deterministic and isolated.</summary>
    [Fact]
    public void AllocatorProducesDeterministicIsolatedResources()
    {
        var allocator = Allocator();
        var profile = Profile() with { LaunchSettings = ArduPilotLaunchSettings.Default with { AdditionalSerialEndpoints = [new ArduPilotSerialEndpoint(1, ArduPilotSerialTransport.UdpClient, "127.0.0.1", 14600)] } };
        var request = new SimulationFleetLaunchRequest(
            profile,
            3,
            SimulationFormationProfile.CreateLine(3, 12.5),
            20,
            2);

        var first = allocator.Allocate(request, []);
        var second = allocator.Allocate(request, []);

        first.Should().BeEquivalentTo(second);
        first.Select(item => item.Profile.EffectiveLaunchSettings.Instance).Should().Equal(0, 1, 2);
        first.Select(item => item.Profile.EffectiveLaunchSettings.SystemId).Should().Equal((byte)1, (byte)2, (byte)3);
        first.Select(item => item.Profile.Endpoints[0].Port).Should().Equal(14550, 14570, 14590);
        first.Select(item => item.Profile.EffectiveLaunchSettings.EffectiveSerialEndpoints[0].Port)
            .Should().Equal(14600, 14620, 14640);
        first.Select(item => item.Profile.Location.LatitudeDegrees).Distinct().Should().HaveCount(3);
        first.Select(item => item.Artifacts.InstanceRootDirectory).Distinct().Should().HaveCount(3);
    }

    /// <summary>Verifies existing vehicle identities and occupied endpoint sets are rejected atomically.</summary>
    [Fact]
    public void AllocatorRejectsIdentityAndEndpointCollisions()
    {
        var vehicleRegistry = Substitute.For<IVehicleRegistry>();
        var connectedVehicle = Vehicle(new VehicleId(1, 1));
        vehicleRegistry.Vehicles.Returns([connectedVehicle]);
        var allocator = Allocator(vehicleRegistry);
        var request = new SimulationFleetLaunchRequest(
            Profile(),
            2,
            SimulationFormationProfile.CreateLine(2, 10));

        var action = () => allocator.Allocate(request, []);

        action.Should().Throw<SimulationAllocationException>().WithMessage("*SystemId 1*");

        vehicleRegistry.Vehicles.Returns([]);
        var occupied = allocator.Allocate(request with { Count = 1, Formation = SimulationFormationProfile.CreateLine(1, 10) }, []);
        var endpointCollision = () => allocator.Allocate(request, occupied);
        endpointCollision.Should().Throw<SimulationAllocationException>();
    }

    /// <summary>Verifies acknowledged commands use the channel registered for the exact simulator vehicle.</summary>
    [Fact]
    public async Task CommandRoutingDoesNotCrossVehicleChannels()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var vehicleId = new VehicleId(23, 1);
        var registry = Substitute.For<IVehicleRegistry>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var registeredVehicle = Vehicle(vehicleId, clock);
        registry.GetRequired(vehicleId).Returns(registeredVehicle);
        var defaultConnection = Substitute.For<IMavLinkConnection>();
        var exactConnection = Substitute.For<IMavLinkConnection>();
        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        connectionSession.Connection.Returns(exactConnection);
        var channels = new SimulationVehicleChannelRegistry();
        channels.Register(new SimulationVehicleChannel(
            Guid.NewGuid(),
            vehicleId,
            connectionSession,
            Profile(),
            now));
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        encoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>())
            .Returns([1, 2, 3]);
        var tracker = Substitute.For<ICommandAckTracker>();
        tracker.WaitForAckAsync(vehicleId, MavLinkCommandIds.ComponentArmDisarm, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new CommandAckMessage(23, 1, new TransportEndPoint("simulation"), MavLinkCommandIds.ComponentArmDisarm, 0, now));
        var policy = Substitute.For<IVehicleCommandPolicy>();
        policy.Evaluate(Arg.Any<VehicleState>(), Arg.Any<VehicleAction>()).Returns(VehicleCommandDecision.Allow());
        var service = new VehicleCommandService(
            registry,
            Substitute.For<IDomainEventHub>(),
            defaultConnection,
            encoder,
            tracker,
            clock,
            policy,
            new ArduPilotModeCatalog(),
            simulationChannels: channels);

        var response = await service.ArmAsync(vehicleId, cancellationToken);

        response.Result.Should().Be(VehicleCommandResult.Accepted);
        await exactConnection.Received(1).SendRawAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<TransportEndPoint>(),
            cancellationToken);
        await defaultConnection.DidNotReceive().SendRawAsync(
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<TransportEndPoint>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies concurrent transports share one inbound dispatcher subscription.</summary>
    [Fact]
    public async Task MessagePumpCoordinatorReferenceCountsOneSharedDispatcher()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var factory = Substitute.For<IServiceFactory>();
        var pump = Substitute.For<IVehicleMessagePump>();
        factory.Create<IVehicleMessagePump>().Returns(pump);
        var coordinator = new VehicleMessagePumpCoordinator(factory);

        var first = await coordinator.AcquireAsync(cancellationToken);
        var second = await coordinator.AcquireAsync(cancellationToken);
        await first.DisposeAsync();

        await pump.Received(1).StartAsync(CancellationToken.None);
        await pump.DidNotReceive().DisposeAsync();

        await second.DisposeAsync();
        await pump.Received(1).DisposeAsync();
    }

    /// <summary>Verifies partial starts are reported per member and start concurrency remains bounded.</summary>
    [Fact]
    public async Task FleetStartReportsPartialFailureWithBoundedConcurrency()
    {
        var probe = new ConcurrencyProbe();
        var managers = Enumerable.Range(0, 4).Select(_ => new FakeSessionManager(probe)).ToArray();
        managers[2].StartFailure = "Instance failed to bind.";
        await using var fleet = Fleet(managers);
        var request = new SimulationFleetLaunchRequest(
            Profile(),
            4,
            SimulationFormationProfile.CreateGrid(4, 10),
            MaximumConcurrency: 2);

        var report = await fleet.StartAllAsync(request, TestContext.Current.CancellationToken);

        report.Results.Should().HaveCount(4);
        report.Results.Count(result => result.Succeeded).Should().Be(3);
        report.Results.Single(result => !result.Succeeded).Error.Should().Contain("failed to bind");
        probe.Maximum.Should().BeLessThanOrEqualTo(2);
        fleet.Sessions.Should().HaveCount(4);
    }

    /// <summary>Verifies a per-session stop failure does not prevent unrelated members from stopping.</summary>
    [Fact]
    public async Task FleetStopReportsPartialFailureWithoutCollapsingOtherMembers()
    {
        var managers = Enumerable.Range(0, 3).Select(_ => new FakeSessionManager()).ToArray();
        await using var fleet = Fleet(managers);
        await fleet.StartAllAsync(
            new SimulationFleetLaunchRequest(Profile(), 3, SimulationFormationProfile.CreateLine(3, 10)),
            TestContext.Current.CancellationToken);
        managers[1].StopFailure = "Process would not stop.";

        var report = await fleet.StopAllAsync(2, TestContext.Current.CancellationToken);

        report.Results.Count(result => result.Succeeded).Should().Be(2);
        managers[0].Current.State.Should().Be(SimulationSessionState.Stopped);
        managers[2].Current.State.Should().Be(SimulationSessionState.Stopped);
        managers[1].Current.State.Should().Be(SimulationSessionState.Running);
    }

    /// <summary>Verifies one runtime crash updates only its member and leaves selection and peers intact.</summary>
    [Fact]
    public async Task FleetCrashIsIsolatedToExactMember()
    {
        var managers = Enumerable.Range(0, 2).Select(_ => new FakeSessionManager()).ToArray();
        await using var fleet = Fleet(managers);
        await fleet.StartAllAsync(
            new SimulationFleetLaunchRequest(Profile(), 2, SimulationFormationProfile.CreateLine(2, 10)),
            TestContext.Current.CancellationToken);
        var selected = fleet.Sessions[1].Allocation.FleetSessionId;
        fleet.Select(selected);

        managers[0].Crash("Unexpected exit 17.");

        fleet.Sessions[0].Session.State.Should().Be(SimulationSessionState.Failed);
        fleet.Sessions[1].Session.State.Should().Be(SimulationSessionState.Running);
        fleet.SelectedSessionId.Should().Be(selected);
    }

    /// <summary>Verifies output and change events remain attached to the member that produced them.</summary>
    [Fact]
    public async Task FleetOutputAndEventsRemainSessionIsolated()
    {
        var managers = Enumerable.Range(0, 2).Select(_ => new FakeSessionManager()).ToArray();
        await using var fleet = Fleet(managers);
        await fleet.StartAllAsync(
            new SimulationFleetLaunchRequest(Profile(), 2, SimulationFormationProfile.CreateLine(2, 10)),
            TestContext.Current.CancellationToken);
        var changed = new List<Guid>();
        fleet.Changed += (_, args) => changed.Add(args.Session.Allocation.FleetSessionId);
        var firstId = fleet.Sessions[0].Allocation.FleetSessionId;

        managers[0].WriteOutput("instance-zero");

        fleet.Sessions[0].Session.RecentOutput.Select(line => line.Text).Should().ContainSingle("instance-zero");
        fleet.Sessions[1].Session.RecentOutput.Should().BeEmpty();
        changed.Should().ContainSingle().Which.Should().Be(firstId);
    }

    /// <summary>Verifies persisted process ownership is recovered only by a new application lifetime.</summary>
    [Fact]
    public async Task OwnershipStoreRecoversPersistedOrphanAndClearsMarker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "MissionPlanner-tests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new SimulationWorkspaceOptions { LogRootDirectory = root });
        var recovery = Substitute.For<ISimulatorOwnedProcessRecovery>();
        var marker = new SimulationOwnedProcess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4242,
            Path.GetFullPath("arducopter-owned.exe"),
            DateTimeOffset.UtcNow);
        var firstLifetime = new SimulationOwnershipStore(
            options,
            recovery,
            Substitute.For<ILogger<SimulationOwnershipStore>>());
        await firstLifetime.MarkAsync(marker, cancellationToken);
        (await firstLifetime.RecoverOrphansAsync(cancellationToken)).Should().BeEmpty();
        recovery.RecoverAsync(marker, cancellationToken).Returns(new SimulationOrphanRecoveryResult(
            marker,
            SimulationOrphanRecoveryState.Recovered,
            "Recovered."));
        var secondLifetime = new SimulationOwnershipStore(
            options,
            recovery,
            Substitute.For<ILogger<SimulationOwnershipStore>>());

        var result = await secondLifetime.RecoverOrphansAsync(cancellationToken);
        var repeated = await secondLifetime.RecoverOrphansAsync(cancellationToken);

        result.Should().ContainSingle(item => item.State == SimulationOrphanRecoveryState.Recovered);
        repeated.Should().BeEmpty();
    }

    /// <summary>Verifies PID reuse protection leaves a non-matching live process untouched.</summary>
    [Fact]
    public async Task LocalRecoveryRefusesMismatchedLiveProcessIdentity()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        var marker = new SimulationOwnedProcess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            current.Id,
            Path.GetFullPath("definitely-not-the-test-host.exe"),
            new DateTimeOffset(current.StartTime.ToUniversalTime()));
        var recovery = new LocalSimulatorOwnedProcessRecovery();

        var result = await recovery.RecoverAsync(marker, TestContext.Current.CancellationToken);

        result.State.Should().Be(SimulationOrphanRecoveryState.IdentityMismatch);
        current.HasExited.Should().BeFalse();
    }

    private static SimulationFleetAllocator Allocator(IVehicleRegistry? registry = null)
    {
        return new SimulationFleetAllocator(
            registry ?? EmptyRegistry(),
            Options.Create(new SimulationWorkspaceOptions { LogRootDirectory = Path.Combine(Path.GetTempPath(), "MissionPlanner-tests", "fleet") }));
    }

    private static IVehicleRegistry EmptyRegistry()
    {
        var registry = Substitute.For<IVehicleRegistry>();
        registry.Vehicles.Returns([]);
        return registry;
    }

    private static SimulationFleetManager Fleet(IReadOnlyList<FakeSessionManager> managers)
    {
        var factory = Substitute.For<ISimulationSessionManagerFactory>();
        var index = 0;
        factory.Create().Returns(_ => managers[index++]);
        var ownership = Substitute.For<ISimulationOwnershipStore>();
        ownership.RecoverOrphansAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SimulationOrphanRecoveryResult>>([]));
        return new SimulationFleetManager(
            Allocator(),
            factory,
            ownership,
            Substitute.For<ILogger<SimulationFleetManager>>());
    }

    private static SimulatorProfile Profile()
    {
        return SimulatorProfile.CreateDefault() with { Id = Guid.Parse("ef17e00a-d6d2-4ff1-b2d0-15cb13397819"), Binary = new SimulatorBinaryReference("test", Path.GetFullPath("arducopter-test.exe"), "test") };
    }

    private static VehicleSession Vehicle(VehicleId id, IDateTimeProvider? suppliedClock = null)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(
            id,
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            now,
            VehicleMode.Stabilize,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var clock = suppliedClock ?? Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new VehicleSession(state, new TransportEndPoint($"vehicle-{id.SystemId}"), clock);
    }

    private sealed class ConcurrencyProbe
    {
        private int current;
        private int maximum;

        public int Maximum => Volatile.Read(ref maximum);

        public async Task EnterAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref current);
            var observed = Volatile.Read(ref maximum);
            while (active > observed)
            {
                Interlocked.CompareExchange(ref maximum, active, observed);
                observed = Volatile.Read(ref maximum);
            }

            try
            {
                await Task.Delay(25, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref current);
            }
        }
    }

    private sealed class FakeSessionManager(ConcurrencyProbe? probe = null) : ISimulationSessionManager
    {
        public string? StartFailure { get; set; }

        public string? StopFailure { get; set; }

        public SimulationSessionSnapshot Current { get; private set; } = SimulationSessionSnapshot.Stopped;

        public event EventHandler<SimulationSessionChangedEventArgs>? Changed;

        public async Task<SimulationSessionSnapshot> StartAsync(
            SimulatorProfile profile,
            CancellationToken cancellationToken = default)
        {
            if (probe is not null)
            {
                await probe.EnterAsync(cancellationToken);
            }

            var failed = StartFailure is not null;
            Current = new SimulationSessionSnapshot(
                Guid.NewGuid(),
                profile,
                failed ? SimulationSessionState.Failed : SimulationSessionState.Running,
                new SimulatorRuntimeIdentity($"fake-{profile.EffectiveLaunchSettings.Instance}", "fake", profile.EffectiveLaunchSettings.Instance + 1000),
                profile.Endpoints,
                DateTimeOffset.UtcNow,
                failed ? DateTimeOffset.UtcNow : null,
                failed ? "Start failed." : "Running.",
                StartFailure,
                [],
                failed ? null : new VehicleId(profile.EffectiveLaunchSettings.SystemId, 1));
            Changed?.Invoke(this, new SimulationSessionChangedEventArgs(Current));
            return Current;
        }

        public Task<SimulationSessionSnapshot> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StopFailure is not null)
            {
                throw new InvalidOperationException(StopFailure);
            }

            Current = Current with { State = SimulationSessionState.Stopped, EndedAt = DateTimeOffset.UtcNow, Message = "Stopped.", Failure = null };
            Changed?.Invoke(this, new SimulationSessionChangedEventArgs(Current));
            return Task.FromResult(Current);
        }

        public Task<SimulationSessionSnapshot> RestartAsync(CancellationToken cancellationToken = default)
        {
            return Current.Profile is { } profile
                ? StartAsync(profile, cancellationToken)
                : Task.FromResult(Current);
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            return StopAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void Crash(string message)
        {
            Current = Current with { State = SimulationSessionState.Failed, EndedAt = DateTimeOffset.UtcNow, Message = "Runtime exited unexpectedly.", Failure = message };
            Changed?.Invoke(this, new SimulationSessionChangedEventArgs(Current));
        }

        public void WriteOutput(string text)
        {
            Current = Current with { RecentOutput = [new SimulatorOutputLine(DateTimeOffset.UtcNow, SimulatorOutputStream.StandardOutput, text)] };
            Changed?.Invoke(this, new SimulationSessionChangedEventArgs(Current));
        }
    }
}
