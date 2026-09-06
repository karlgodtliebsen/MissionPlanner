using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulator;
using MissionPlanner.Test.Support.Configuration;
using MissionPlanner.Smoke.Simulator.Tests.SmokeTests;
using MissionPlanner.Transport;

namespace MissionPlanner.Smoke.Simulator.Tests;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class DomainVehicleServiceSimulatorTests : IAsyncDisposable
{
    private readonly ITestOutputHelper output;
    private readonly ServiceProvider serviceProvider;
    private readonly IPEndPoint simulatorIPEndPoint;
    private readonly int port;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainVehicleServiceSimulatorTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public DomainVehicleServiceSimulatorTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration(output);
        services.ConfigureIsolatedUdpTransport();
        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();

        var logger = serviceProvider.GetRequiredService<ILogger<DomainVehicleServiceSimulatorTests>>();
        var endPoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>().Value;

        logger.LogInformation($"Test configuration initialized. UDP local:  {endPoint.LocalHost}:{endPoint.LocalPort}");
        logger.LogInformation($"Test configuration initialized. UDP remote: {endPoint.RemoteHost}:{endPoint.RemotePort}");

        var targetPort = endPoint.LocalPort;
        var targetIp = endPoint.RemoteHost;

        var targetAddress = string.IsNullOrWhiteSpace(endPoint.RemoteHost)
            ? IPAddress.Any
            : IPAddress.Parse(targetIp);
        port = endPoint.RemotePort;
        simulatorIPEndPoint = new IPEndPoint(targetAddress, targetPort);
    }

    /// <summary>
    /// Tests that a vehicle can be armed through IVehicleService using the full MAVLink simulator pipeline.
    /// </summary>
    [Fact]
    public async Task Should_Arm_Vehicle_Through_VehicleService_When_Command_Is_Acked()
    {
        //   var endpoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>().Value;

        var vehicleId = new VehicleId(1, 1);

        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();

        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        using var lifetime = await session.CreateUdpConnection(simulatorIPEndPoint.Port, "127.0.0.1", port, cancellationToken: TestContext.Current.CancellationToken);

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100)
            );

        await simulator.StartAsync(TestContext.Current.CancellationToken);
        await WaitForRegisteredVehicle();

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId);
                Assert.NotNull(state);

                Assert.Equal(vehicleId, state.VehicleId);
                Assert.False(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, response.Result);

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId);
                Assert.NotNull(state);
                Assert.True(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tests that a vehicle can be disarmed through IVehicleService using the full MAVLink simulator pipeline.
    /// </summary>
    [Fact]
    public async Task Should_Disarm_Vehicle_Through_VehicleService_When_Command_Is_Acked()
    {
        // var endpoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>().Value;
        var vehicleId = new VehicleId(1, 1);
        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100)
            );

        using var lifetime = await session.CreateUdpConnection(simulatorIPEndPoint.Port, "127.0.0.1", port, cancellationToken: TestContext.Current.CancellationToken);
        await simulator.StartAsync(TestContext.Current.CancellationToken);
        await WaitForRegisteredVehicle();

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId);
                Assert.NotNull(state);
                Assert.False(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var armResponse = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, armResponse.Result);

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId);
                Assert.NotNull(state);
                Assert.True(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var disarmResponse = await vehicleService.DisarmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, disarmResponse.Result);

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId);
                Assert.NotNull(state);
                Assert.False(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tests that the vehicle service correctly sets the vehicle mode to Guided when the command is acknowledged.
    /// </summary>
    [Fact]
    public async Task Should_Set_Guided_Mode_Through_VehicleService_When_Command_Is_Acked()
    {
        var vehicleId = new VehicleId(1, 1);

        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();

        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        using var lifetime = await session.CreateUdpConnection(simulatorIPEndPoint.Port, "127.0.0.1", port, cancellationToken: TestContext.Current.CancellationToken);

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100)
            );

        await simulator.StartAsync(TestContext.Current.CancellationToken);
        await WaitForRegisteredVehicle();

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId)!;
                Assert.Equal(VehicleMode.Stabilize, state.Mode);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var armResponse = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, armResponse.Result);

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId)!;
                Assert.True(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var response = await vehicleService.SetModeAsync(vehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a timeout when the arm command is not acknowledged.
    /// </summary>
    [Fact]
    public async Task Should_Return_Timeout_When_Arm_Command_Is_Not_Acked()
    {
        var vehicleId = new VehicleId(1, 1);

        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();

        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        using var lifetime = await session.CreateUdpConnection(simulatorIPEndPoint.Port, "127.0.0.1", port, cancellationToken: TestContext.Current.CancellationToken);

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100),
                false);

        await simulator.StartAsync(TestContext.Current.CancellationToken);
        await WaitForRegisteredVehicle();

        await EventuallyAsync(
            () =>
            {
                var state = vehicleService.GetVehicleState(vehicleId);
                Assert.NotNull(state);
                Assert.Equal(vehicleId, state!.VehicleId);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Timeout, response.Result);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a denied result when the arm command is denied.
    /// </summary>
    [Fact]
    public async Task Should_Return_Denied_When_Arm_Command_Is_Denied()
    {
        var vehicleId = new VehicleId(1, 1);

        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();

        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        using var lifetime = await session.CreateUdpConnection(simulatorIPEndPoint.Port, "127.0.0.1", port, cancellationToken: TestContext.Current.CancellationToken);

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100),
                true,
                2); // MAV_RESULT_DENIED

        await simulator.StartAsync(TestContext.Current.CancellationToken);
        await WaitForRegisteredVehicle();

        await EventuallyAsync(
            () => vehicleService.GetVehicleState(vehicleId),
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var testVehicle = await WaitForRegisteredVehicle();

        var response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Denied, response.Result);
        Assert.Equal("MAVLink ACK result 2.", response.Message);

        var state = vehicleService.GetVehicleState(vehicleId);
        Assert.NotNull(state);
        Assert.False(state!.IsArmed);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a denied result when the vehicle is offline.
    /// </summary>
    [Fact]
    public async Task Should_Deny_Arm_When_Vehicle_Is_Offline()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        var vehicleId = new VehicleId(1, 1);
        var receivedAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        var vehicle = await registry.RegisterOrUpdateHeartbeatAsync(
            vehicleId,
            simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt, TestContext.Current.CancellationToken);

        await registry.UpdateConnectionStates(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(VehicleConnectionState.Offline, vehicle.Vehicle.State.ConnectionState);
        var response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Denied, response.Result);
    }

    [Fact]
    public async Task Should_Set_Guided_Mode_When_Vehicle_Is_Not_Armed()
    {
        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();
        using var lifetime = await session.CreateUdpConnection(simulatorIPEndPoint.Port, "127.0.0.1", port, cancellationToken: TestContext.Current.CancellationToken);
        await using var simulator = new FakeMavLinkVehicle2(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
            "127.0.0.1", simulatorIPEndPoint.Port, port, TimeSpan.FromMilliseconds(100));
        await simulator.StartAsync(TestContext.Current.CancellationToken);
        var state = await WaitForRegisteredVehicle();
        Assert.False(state.IsArmed);
        var service = serviceProvider.GetRequiredService<IVehicleService>();
        var response = await service.SetModeAsync(state.VehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        Assert.False(service.GetVehicleState(state.VehicleId)!.IsArmed);
    }
    [Fact]
    public async Task ConnectionServiceConnectsAndDisconnectsUdpVehicle()
    {
        await using var simulator = new FakeMavLinkVehicle2(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
            "127.0.0.1", simulatorIPEndPoint.Port, port, TimeSpan.FromMilliseconds(100));
        await simulator.StartAsync(TestContext.Current.CancellationToken);
        var service = serviceProvider.GetRequiredService<IVehicleConnectionService>();
        var result = await service.ConnectUdpAsync(simulatorIPEndPoint.Port, "127.0.0.1", port, TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(service.IsConnected);
        await service.DisconnectAsync(TestContext.Current.CancellationToken);
        Assert.False(service.IsConnected);
    }

    private async Task<VehicleState> WaitForRegisteredVehicle()
    {
        var logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();

        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        VehicleState? testVehicle = null;
        await EventuallyAsync(
            () =>
            {
                var vehicles = vehicleService.GetVehicles();
                Assert.NotEmpty(vehicles);
                testVehicle = vehicles.First();
                logger.LogTrace("Vehicle: {VehicleId}, State: {ConnectionState}, Mode: {Mode}", testVehicle.VehicleId, testVehicle.ConnectionState, testVehicle.Mode);
                Assert.Equal(VehicleConnectionState.Online, testVehicle.ConnectionState);
                Assert.Equal(VehicleLandedState.OnGround, testVehicle.Flight.LandedState);
                ts.TrySetResult();
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        testVehicle.Should().NotBeNull();
        DomainException.ThrowIfNull(testVehicle);

        return testVehicle!;
    }

    private static async Task EventuallyAsync(Action assertion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                assertion();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(50, cancellationToken);
            }
        }

        throw lastException ?? new TimeoutException();
    }
    public ValueTask DisposeAsync() => serviceProvider.DisposeAsync();
}
