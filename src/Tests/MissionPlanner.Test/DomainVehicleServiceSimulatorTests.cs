using System.Net;

using Domain.Library;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Simulator;
using MissionPlanner.Test.Configuration;
using MissionPlanner.Test.SmokeTests;
using MissionPlanner.Transport;

namespace MissionPlanner.Test;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class DomainVehicleServiceSimulatorTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;
    private readonly IPEndPoint simulatorIPEndPoint;
    private readonly int port;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainVehicleServiceSimulatorTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public DomainVehicleServiceSimulatorTests(ITestOutputHelper output)
    {
        this.output = output;
        IServiceCollection services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);
        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();

        ILogger<DomainVehicleServiceSimulatorTests> logger = serviceProvider.GetRequiredService<ILogger<DomainVehicleServiceSimulatorTests>>();
        TransportEndpoint endPoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>().Value;

        logger.LogInformation($"Test configuration initialized. UDP local:  {endPoint.LocalHost}:{endPoint.LocalPort}");
        logger.LogInformation($"Test configuration initialized. UDP remote: {endPoint.RemoteHost}:{endPoint.RemotePort}");

        var targetPort = endPoint.LocalPort;
        var targetIp = endPoint.RemoteHost;

        IPAddress targetAddress = string.IsNullOrWhiteSpace(endPoint.RemoteHost)
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

        await using IMavLinkConnection connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        IVehicleMessagePump messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await connection.StartAsync(TestContext.Current.CancellationToken);


        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100)
            );

        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await simulator.StartAsync(TestContext.Current.CancellationToken);

        await EventuallyAsync(
            () =>
            {
                VehicleState? state = vehicleService.GetVehicleState(vehicleId);

                Assert.Equal(vehicleId, state.VehicleId);
                Assert.False(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, response.Result);

        await EventuallyAsync(
            () =>
            {
                VehicleState? state = vehicleService.GetVehicleState(vehicleId);
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
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        IVehicleMessagePump messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();
        await using IMavLinkConnection connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100)
            );

        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => simulator.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await EventuallyAsync(
            () =>
            {
                VehicleState? state = vehicleService.GetVehicleState(vehicleId);
                Assert.False(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse armResponse = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, armResponse.Result);

        await EventuallyAsync(
            () =>
            {
                VehicleState? state = vehicleService.GetVehicleState(vehicleId);
                Assert.True(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse disarmResponse = await vehicleService.DisarmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, disarmResponse.Result);

        await EventuallyAsync(
            () =>
            {
                VehicleState? state = vehicleService.GetVehicleState(vehicleId);
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

        await using IMavLinkConnection connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        IVehicleMessagePump messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await connection.StartAsync(TestContext.Current.CancellationToken);

        var pump = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

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

        await EventuallyAsync(
            () =>
            {
                VehicleState state = vehicleService.GetVehicleState(vehicleId)!;
                Assert.Equal(VehicleMode.Stabilize, state.Mode);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse armResponse = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, armResponse.Result);

        await EventuallyAsync(
            () =>
            {
                VehicleState state = vehicleService.GetVehicleState(vehicleId)!;
                Assert.True(state.IsArmed);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse response = await vehicleService.SetModeAsync(vehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a timeout when the arm command is not acknowledged.
    /// </summary>
    [Fact]
    public async Task Should_Return_Timeout_When_Arm_Command_Is_Not_Acked()
    {
        var vehicleId = new VehicleId(1, 1);

        await using IMavLinkConnection connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        IVehicleMessagePump messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await connection.StartAsync(TestContext.Current.CancellationToken);

        var pumpTask = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

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

        await EventuallyAsync(
            () =>
            {
                VehicleState? state = vehicleService.GetVehicleState(vehicleId);
                Assert.Equal(vehicleId, state!.VehicleId);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Timeout, response.Result);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a denied result when the arm command is denied.
    /// </summary>
    [Fact]
    public async Task Should_Return_Denied_When_Arm_Command_Is_Denied()
    {
        var vehicleId = new VehicleId(1, 1);

        await using IMavLinkConnection connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        IVehicleMessagePump messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await connection.StartAsync(TestContext.Current.CancellationToken);

        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

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

        await EventuallyAsync(
            () => vehicleService.GetVehicleState(vehicleId),
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VehicleState testVehicle = await WaitForRegisteredVehicle();

        VehicleCommandResponse response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Denied, response.Result);

        VehicleState? state = vehicleService.GetVehicleState(vehicleId);
        Assert.False(state.IsArmed);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a denied result when the vehicle is offline.
    /// </summary>
    [Fact]
    public async Task Should_Deny_Arm_When_Vehicle_Is_Offline()
    {
        IVehicleRegistry registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        var vehicleId = new VehicleId(1, 1);
        DateTimeOffset receivedAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        VehicleRegistryResult vehicle = registry.RegisterOrUpdateHeartbeat(
            vehicleId,
            simulatorIPEndPoint,
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt);

        registry.UpdateConnectionStates(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        Assert.Equal(VehicleConnectionState.Offline, vehicle.Vehicle.State.ConnectionState);
        VehicleCommandResponse response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Denied, response.Result);
    }

    /// <summary>
    /// Tests that the vehicle service correctly returns a denied result when attempting to set guided mode while the vehicle is not armed.
    /// </summary>
    [Fact]
    public async Task Should_Deny_Guided_Mode_When_Vehicle_Is_Not_Armed()
    {
        IVehicleRegistry registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        var vehicleId = new VehicleId(1, 1);

        registry.RegisterOrUpdateHeartbeat(
            vehicleId,
            simulatorIPEndPoint,
            0,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        VehicleCommandResponse response = await vehicleService.SetModeAsync(vehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Denied, response.Result);
    }

    private async Task<VehicleState> WaitForRegisteredVehicle()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        VehicleState? testVehicle = null;
        await EventuallyAsync(
            () =>
            {
                IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();
                Assert.NotEmpty(vehicles);
                testVehicle = vehicles.First();
                logger.LogTrace("Vehicle: {VehicleId}, State: {ConnectionState}, Mode: {Mode}", testVehicle.VehicleId, testVehicle.ConnectionState, testVehicle.Mode);
                Assert.Equal(VehicleConnectionState.Online, testVehicle.ConnectionState);
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
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
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
}