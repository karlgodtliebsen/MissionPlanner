using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies typed ArduPilot SITL launch, allocation, connection, and cleanup behavior.</summary>
public sealed class ArduPilotSitlRuntimeTests
{
    /// <summary>Verifies typed arguments preserve spaces as tokens and format values invariantly.</summary>
    [Fact]
    public void LaunchPlanUsesTypedTokensWithoutShellEscaping()
    {
        var defaultsPath = Path.GetFullPath(Path.Combine("directory with spaces", "defaults file.parm"));
        var profile = Profile() with
        {
            Location = new SimulationLocation(55.1234567, 12.7654321, 42.5, 91.25),
            Speedup = 2.5,
            AdditionalArguments = ["--custom-option", "value with spaces"],
            Environment = new Dictionary<string, string> { ["SITL_TEST"] = "environment value" },
            LaunchSettings = new ArduPilotLaunchSettings(
                3,
                17,
                [defaultsPath],
                true,
                false,
                true,
                [new ArduPilotSerialEndpoint(2, ArduPilotSerialTransport.TcpClient, "127.0.0.1", 5762)])
        };

        var plan = new ArduPilotLaunchPlanBuilder(new ArduPilotFrameCatalog())
            .Build(profile, Path.GetFullPath("runtime directory"));

        plan.Arguments.Should().ContainInOrder(
            "--model", "quad",
            "--home", "55.1234567,12.7654321,42.5,91.25",
            "--speedup", "2.5",
            "--instance", "3",
            "--sysid", "17",
            "--serial0", "udpclient:127.0.0.1:14550",
            "--serial2", "tcpclient:127.0.0.1:5762",
            "--defaults", defaultsPath,
            "--wipe",
            "--custom-option", "value with spaces");
        plan.Arguments.Should().ContainSingle(item => item == "value with spaces");
        plan.Environment["SITL_TEST"].Should().Be("environment value");
    }

    /// <summary>Verifies free-form arguments cannot override identity or endpoint settings.</summary>
    [Theory]
    [InlineData("--sysid=99")]
    [InlineData("--serial0=udpclient:127.0.0.1:9999")]
    [InlineData("-I4")]
    [InlineData("--home")]
    public void LaunchPlanRejectsTypedSettingOverrides(string argument)
    {
        var profile = Profile() with { AdditionalArguments = [argument] };

        var action = () => new ArduPilotLaunchPlanBuilder(new ArduPilotFrameCatalog())
            .Build(profile, Path.GetFullPath("runtime"));

        action.Should().Throw<InvalidOperationException>().WithMessage("*override*");
    }

    /// <summary>Verifies supported frame/model choices are scoped to each firmware family.</summary>
    [Theory]
    [InlineData(FirmwareFamily.ArduCopter, "quad")]
    [InlineData(FirmwareFamily.ArduPlane, "plane")]
    [InlineData(FirmwareFamily.Rover, "rover")]
    [InlineData(FirmwareFamily.ArduSub, "vectored")]
    public void FrameCatalogSupportsEachRequiredFamily(FirmwareFamily family, string frame)
    {
        var catalog = new ArduPilotFrameCatalog();

        catalog.IsSupported(family, frame).Should().BeTrue();
        catalog.GetFrames(family).Should().Contain(frame);
    }

    /// <summary>Verifies endpoint leases reject collisions, allow distinct instances, and release exactly.</summary>
    [Fact]
    public async Task PortAllocatorPreventsOwnedSessionCollisions()
    {
        var host = Substitute.For<ISimulatorHostEnvironment>();
        host.IsPortAvailableAsync(Arg.Any<SimulationEndpoint>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        var allocator = new SimulationPortAllocator(host);
        var firstEndpoints = Endpoints(14550, 5760);
        var secondEndpoints = Endpoints(14560, 5770);
        await using var first = await allocator.ReserveAsync(firstEndpoints, TestContext.Current.CancellationToken);
        await using var second = await allocator.ReserveAsync(secondEndpoints, TestContext.Current.CancellationToken);

        var collision = () => allocator.ReserveAsync(firstEndpoints, TestContext.Current.CancellationToken).AsTask();

        await collision.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already reserved*");
        await first.DisposeAsync();
        await using var replacement = await allocator.ReserveAsync(firstEndpoints, TestContext.Current.CancellationToken);
        replacement.Endpoints.Should().BeEquivalentTo(firstEndpoints);
    }

    /// <summary>Verifies a successful heartbeat must match both SystemId and firmware family.</summary>
    [Fact]
    public async Task VehicleConnectionAcceptsExpectedHeartbeatIdentity()
    {
        var vehicleId = new VehicleId(7, 1);
        var connectionId = Guid.NewGuid();
        var service = Substitute.For<IVehicleConnectionService>();
        service.ConnectUdpExclusiveAsync(14550, "127.0.0.1", 14550, Arg.Any<CancellationToken>())
            .Returns(new VehicleConnectionResult(true, vehicleId, null, ConnectionId: connectionId));
        var registry = Substitute.For<IVehicleRegistry>();
        var vehicleSession = Session(vehicleId, FirmwareFamily.ArduCopter);
        registry.GetRequired(vehicleId).Returns(vehicleSession);
        var connection = new SimulatorVehicleConnection(
            service,
            registry,
            Substitute.For<ILogger<SimulatorVehicleConnection>>());
        var profile = Profile() with
        {
            LaunchSettings = ArduPilotLaunchSettings.Default with { SystemId = 7 }
        };

        var connected = await connection.ConnectAsync(
            profile,
            profile.Endpoints[0],
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        connected.Should().Be(vehicleId);
        await connection.DisconnectAsync(TestContext.Current.CancellationToken);
        await service.Received(1).DisconnectOwnedAsync(connectionId, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies wrong-family or wrong-SystemId heartbeats fail and the exact connection is cleaned up.</summary>
    /// <param name="actualSystemId">Observed system ID.</param>
    /// <param name="actualFamily">Observed firmware family.</param>
    [Theory]
    [InlineData(1, FirmwareFamily.ArduPlane)]
    [InlineData(2, FirmwareFamily.ArduCopter)]
    public async Task VehicleConnectionRejectsWrongHeartbeatIdentity(byte actualSystemId, FirmwareFamily actualFamily)
    {
        var vehicleId = new VehicleId(actualSystemId, 1);
        var connectionId = Guid.NewGuid();
        var service = Substitute.For<IVehicleConnectionService>();
        service.ConnectUdpExclusiveAsync(14550, "127.0.0.1", 14550, Arg.Any<CancellationToken>())
            .Returns(new VehicleConnectionResult(true, vehicleId, null, ConnectionId: connectionId));
        var registry = Substitute.For<IVehicleRegistry>();
        var vehicleSession = Session(vehicleId, actualFamily);
        registry.GetRequired(vehicleId).Returns(vehicleSession);
        var connection = new SimulatorVehicleConnection(
            service,
            registry,
            Substitute.For<ILogger<SimulatorVehicleConnection>>());

        var action = () => connection.ConnectAsync(
            Profile(),
            Profile().Endpoints[0],
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<SimulationConnectionException>().WithMessage("*expected system 1 and ArduCopter*");
        await service.Received(1).DisconnectOwnedAsync(connectionId, CancellationToken.None);
    }

    /// <summary>Verifies heartbeat timeout is distinguished from process launch failure.</summary>
    [Fact]
    public async Task VehicleConnectionReportsHeartbeatTimeout()
    {
        var service = Substitute.For<IVehicleConnectionService>();
        service.ConnectUdpExclusiveAsync(14550, "127.0.0.1", 14550, Arg.Any<CancellationToken>())
            .Returns(call => WaitForeverAsync(call.ArgAt<CancellationToken>(3)));
        var connection = new SimulatorVehicleConnection(
            service,
            Substitute.For<IVehicleRegistry>(),
            Substitute.For<ILogger<SimulatorVehicleConnection>>());

        var action = () => connection.ConnectAsync(
            Profile(),
            Profile().Endpoints[0],
            TimeSpan.FromMilliseconds(25),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<SimulationConnectionException>().WithMessage("*no MAVLink heartbeat*25 milliseconds*");
    }

    /// <summary>Verifies owned shutdown disconnects the MAVLink session before stopping the exact process.</summary>
    [Fact]
    public async Task RuntimeStopsConnectionBeforeOwnedProcessAndReleasesPorts()
    {
        var order = new List<string>();
        var process = new FakeProcessSession(order);
        var processHost = Substitute.For<ISimulatorProcessHost>();
        processHost.StartAsync(Arg.Any<SimulatorProcessStartInfo>(), Arg.Any<CancellationToken>())
            .Returns(process);
        var vehicleConnection = Substitute.For<ISimulatorVehicleConnection>();
        vehicleConnection.ConnectAsync(
                Arg.Any<SimulatorProfile>(),
                Arg.Any<SimulationEndpoint>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new VehicleId(1, 1));
        vehicleConnection.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            order.Add("disconnect");
            return Task.CompletedTask;
        });
        var lease = new FakePortLease(Endpoints(14550, 5760), order);
        var allocator = Substitute.For<ISimulationPortAllocator>();
        allocator.ReserveAsync(Arg.Any<IReadOnlyList<SimulationEndpoint>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ISimulationPortLease>(lease));
        var runtime = Runtime(processHost, vehicleConnection, allocator);
        var profile = Profile();

        await using var session = await runtime.StartAsync(
            new SimulatorStartRequest(Guid.NewGuid(), profile, Path.GetFullPath("sitl-test-runtime")),
            TestContext.Current.CancellationToken);
        await session.WaitForHeartbeatAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        order.Should().Equal("disconnect", "process-stop", "ports-release");
        session.Identity.ProcessId.Should().Be(4242);
    }

    /// <summary>Verifies early process exit includes bounded actionable stderr in the startup failure.</summary>
    [Fact]
    public async Task RuntimeReportsEarlyProcessExitWithRecentStandardError()
    {
        var order = new List<string>();
        var process = new FakeProcessSession(order);
        var processHost = Substitute.For<ISimulatorProcessHost>();
        processHost.StartAsync(Arg.Any<SimulatorProcessStartInfo>(), Arg.Any<CancellationToken>()).Returns(process);
        var vehicleConnection = Substitute.For<ISimulatorVehicleConnection>();
        vehicleConnection.ConnectAsync(
                Arg.Any<SimulatorProfile>(),
                Arg.Any<SimulationEndpoint>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call => WaitForVehicleForeverAsync(call.ArgAt<CancellationToken>(3)));
        var lease = new FakePortLease(Endpoints(14550, 5760), order);
        var allocator = Substitute.For<ISimulationPortAllocator>();
        allocator.ReserveAsync(Arg.Any<IReadOnlyList<SimulationEndpoint>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ISimulationPortLease>(lease));
        var runtime = Runtime(processHost, vehicleConnection, allocator);
        await using var session = await runtime.StartAsync(
            new SimulatorStartRequest(Guid.NewGuid(), Profile(), Path.GetFullPath("sitl-failed-runtime")),
            TestContext.Current.CancellationToken);
        process.WriteError("Failed to bind serial0 to UDP port 14550");
        process.Exit(2, "Startup failed.");

        var action = () => session.WaitForHeartbeatAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<SimulationConnectionException>()
            .WithMessage("*exited before vehicle connection*Recent stderr:*Failed to bind serial0*");
    }

    private static ArduPilotSitlRuntime Runtime(
        ISimulatorProcessHost processHost,
        ISimulatorVehicleConnection vehicleConnection,
        ISimulationPortAllocator allocator)
    {
        var platform = Substitute.For<ISitlPlatformService>();
        platform.Current.Returns(new SitlPlatformCapability(
            SitlPlatform.Windows,
            SitlArchitecture.X64,
            true,
            "Supported."));
        var catalog = new ArduPilotFrameCatalog();
        return new ArduPilotSitlRuntime(
            new ArduPilotLaunchPlanBuilder(catalog),
            catalog,
            allocator,
            processHost,
            vehicleConnection,
            platform,
            Substitute.For<ILogger<ArduPilotSitlRuntime>>());
    }

    private static async Task<VehicleConnectionResult> WaitForeverAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new VehicleConnectionResult(false, null, null);
    }

    private static async Task<VehicleId> WaitForVehicleForeverAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new VehicleId(1, 1);
    }

    private static SimulatorProfile Profile() => SimulatorProfile.CreateDefault() with
    {
        Binary = new SimulatorBinaryReference("4.6.0", Path.GetFullPath("arducopter-test.exe"), "test"),
        LaunchSettings = ArduPilotLaunchSettings.Default
    };

    private static IReadOnlyList<SimulationEndpoint> Endpoints(int mavLinkPort, int consolePort) =>
    [
        new SimulationEndpoint("MAVLink", SimulationEndpointTransport.Udp, "127.0.0.1", mavLinkPort),
        new SimulationEndpoint("Console", SimulationEndpointTransport.Tcp, "127.0.0.1", consolePort)
    ];

    private static VehicleSession Session(VehicleId vehicleId, FirmwareFamily family)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(
            vehicleId,
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            now,
            VehicleMode.Unknown,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        state = state with
        {
            Identity = state.Identity with
            {
                Firmware = state.Identity.Firmware with { Family = family }
            }
        };
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new VehicleSession(state, new TransportEndPoint("test"), clock);
    }

    private sealed class FakeProcessSession(List<string> order) : ISimulatorProcessSession
    {
        private readonly TaskCompletionSource<SimulatorRuntimeExit> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId => 4242;

        public Task<SimulatorRuntimeExit> Completion => completion.Task;

        public IReadOnlyList<SimulatorOutputLine> RecentOutput => [];

        public event EventHandler<SimulatorOutputLine>? OutputReceived;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            order.Add("process-stop");
            completion.TrySetResult(new SimulatorRuntimeExit(0, true, "Stopped."));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            completion.TrySetResult(new SimulatorRuntimeExit(0, true, "Disposed."));
            return ValueTask.CompletedTask;
        }

        public void WriteError(string message) => OutputReceived?.Invoke(
            this,
            new SimulatorOutputLine(DateTimeOffset.UtcNow, SimulatorOutputStream.StandardError, message));

        public void Exit(int exitCode, string message) =>
            completion.TrySetResult(new SimulatorRuntimeExit(exitCode, false, message));
    }

    private sealed class FakePortLease(
        IReadOnlyList<SimulationEndpoint> endpoints,
        List<string> order) : ISimulationPortLease
    {
        private bool disposed;

        public IReadOnlyList<SimulationEndpoint> Endpoints { get; } = endpoints;

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
                order.Add("ports-release");
            }

            return ValueTask.CompletedTask;
        }
    }
}
