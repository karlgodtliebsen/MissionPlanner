using System.Net.Sockets;

using Domain.Library;
using Domain.Library.EventHub.Abstractions;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Test.Configuration;
using MissionPlanner.Transport;

namespace MissionPlanner.Test.SmokeTests;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class SmokeTestsSitl : IAsyncLifetime
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmokeTestsSitl"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public SmokeTestsSitl(ITestOutputHelper output)
    {
        this.output = output;

        IServiceCollection services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();

        IOptions<TransportEndpoint> endPoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>();
        endPoint.Value.RemoteHost = "127.0.0.1";
        endPoint.Value.RemotePort = 14551;

        endPoint.Value.LocalHost = "0.0.0.0";
        endPoint.Value.LocalPort = 14550;

        //for tcp: 127.0.0.1 on port 5760

        ILogger<SmokeTestsDroneBridge> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsDroneBridge>>();

        logger.LogInformation($"Test configuration initialized. UDP local:  {endPoint.Value.LocalHost}:{endPoint.Value.LocalPort}");
        logger.LogInformation($"Test configuration initialized. UDP remote: {endPoint.Value.RemoteHost}:{endPoint.Value.RemotePort}");
    }

    private VehicleId vehicleId;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        await ResetSitlVehicleAsync(vehicleService, CancellationToken.None);
        await connection.DisposeAsync();
        await messagePump.DisposeAsync();
    }

    private IMavLinkConnection connection;
    private IVehicleMessagePump messagePump;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        IMavLinkConnection conn = connection;
        IVehicleMessagePump mp = messagePump;
        _ = Task.Run(() => conn.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => mp.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        vehicleId = new VehicleId(1, 1);
        await ResetSitlVehicleAsync(vehicleService, TestContext.Current.CancellationToken);
    }

    private async Task ResetSitlVehicleAsync(IVehicleService vehicleService, CancellationToken cancellationToken)
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();

        await EventuallyAsync(
            () =>
            {
                IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();

                logger.LogTrace("Vehicle count: {VehicleCount}", vehicles.Count);

                Assert.NotEmpty(vehicles);

                VehicleState vehicle = vehicles.First();

                logger.LogTrace("Vehicle: {VehicleId}, State: {ConnectionState}, Mode: {Mode}", vehicle.VehicleId, vehicle.ConnectionState, vehicle.Mode);

                Assert.Equal(VehicleConnectionState.Online, vehicle.ConnectionState);
            },
            TimeSpan.FromSeconds(10),
            cancellationToken);

        VehicleState? current = vehicleService.GetVehicleState(vehicleId);
        if (current is not null)
        {
            if (current.IsArmed)
            {
                VehicleCommandResponse disarmResponse = await vehicleService.DisarmAsync(vehicleId, cancellationToken);

                Assert.Equal(VehicleCommandResult.Accepted, disarmResponse.Result);

                await EventuallyAsync(
                    () =>
                    {
                        VehicleState? vehicle = vehicleService.GetVehicleState(vehicleId);
                        if (vehicle is not null)
                        {
                            Assert.False(vehicle.IsArmed);
                        }
                    },
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
            }
        }

        VehicleCommandResponse modeResponse = await vehicleService.SetModeAsync(vehicleId, VehicleMode.Stabilize, cancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                VehicleState? vehicle = vehicleService.GetVehicleState(vehicleId);
                if (vehicle is not null)
                {
                    Assert.Equal(VehicleMode.Stabilize, vehicle.Mode);
                    Assert.Equal(VehicleConnectionState.Online, vehicle.ConnectionState);
                }
            },
            TimeSpan.FromSeconds(10),
            cancellationToken);
    }

    /// <summary>
    /// Sends a TCP  probe to the SITL without expecting any response.
    /// </summary>
    [Fact]
    public async Task Should_Send_Tcp_Probe_To_SITL_Without_Error()
    {
        using var tcpClient = new TcpClient();

        await tcpClient.ConnectAsync("127.0.0.1", 5760, TestContext.Current.CancellationToken);

        Assert.True(tcpClient.Connected);
    }

    /// <summary>
    /// Receives a MAVLink heartbeat message through the SITL.
    /// </summary>
    [Fact]
    public async Task Should_Receive_Heartbeat_From_SITL()
    {
        IEventHub eventHub = serviceProvider.GetRequiredService<IEventHub>();

        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MavLinkFrame? messageResult = null;
        using IDisposable subscription = eventHub.SubscribeAsync<MavLinkFrame>(MavLinkEventTopics.ReceivedFrame, (frame, cts) =>
        {
            if (frame.MessageId == MessageIds.Heartbeat)
            {
                messageResult = frame;
                ts.TrySetResult();
            }

            return Task.CompletedTask;
        });

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        messageResult.Should().NotBeNull();
        MavLinkFrame frame = messageResult!;


        Assert.Equal(MessageIds.Heartbeat, frame.MessageId);
        Assert.True(frame.SystemId > 0);
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_From_SITL_Heartbeat()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        await EventuallyAsync(
            () =>
            {
                IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();

                logger.LogTrace("Vehicle count: {VehicleCount}", vehicles.Count);

                Assert.NotEmpty(vehicles);

                VehicleState vehicle = vehicles.First();

                logger.LogTrace("Vehicle: {VehicleId}, State: {ConnectionState}, Mode: {Mode}", vehicle.VehicleId, vehicle.ConnectionState, vehicle.Mode);

                Assert.Equal(VehicleConnectionState.Online, vehicle.ConnectionState);
            },
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_From_SITL_Heartbeat_And_Verify_Telemetry()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        await EventuallyAsync(
            () =>
            {
                IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();

                logger.LogTrace("Vehicle count: {VehicleCount}", vehicles.Count);

                Assert.NotEmpty(vehicles);

                VehicleState vehicle = vehicles.First();

                logger.LogTrace("Vehicle: {VehicleId}, State: {ConnectionState}, Mode: {Mode}", vehicle.VehicleId, vehicle.ConnectionState, vehicle.Mode);

                Assert.Equal(VehicleConnectionState.Online, vehicle.ConnectionState);

                Assert.NotNull(vehicle.Roll);
                Assert.NotNull(vehicle.Pitch);
                Assert.NotNull(vehicle.Yaw);
                Assert.NotNull(vehicle.Latitude);
                Assert.NotNull(vehicle.Longitude);
                Assert.NotNull(vehicle.Altitude);
                Assert.NotNull(vehicle.BatteryVoltage);
            },
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and sends an arm command.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_From_SITL_Heartbeat_And_Send_Arm()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();

        VehicleState testVehicle = await WaitForRegisteredVehicle();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        VehicleCommandResponse response = await vehicleService.ArmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();
        VehicleState vehicle = vehicles.First();
        vehicle.IsArmed.Should().BeTrue();
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and sends an arm command.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_From_SITL_Heartbeat_And_Send_DisArm()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        VehicleState testVehicle = await WaitForRegisteredVehicle();

        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        VehicleCommandResponse response = await vehicleService.ArmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();
        VehicleState vehicle = vehicles.First();
        vehicle.IsArmed.Should().BeTrue();


        response = await vehicleService.DisarmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Disarm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        vehicles = vehicleService.GetVehicles();
        vehicle = vehicles.First();
        vehicle.IsArmed.Should().BeFalse();
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and sends a set mode command to guided.
    /// </summary>
    [Fact]
    public async Task Should_Set_Guided_Mode_Through_SITL()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        VehicleState testVehicle = await WaitForRegisteredVehicle();
        VehicleCommandResponse response = await vehicleService.ArmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();
        VehicleState vehicle = vehicles.First();
        vehicle.IsArmed.Should().BeTrue();


        response = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);


        vehicles = vehicleService.GetVehicles();
        vehicle = vehicles.First();
        vehicle.Mode.Should().Be(VehicleMode.Guided);
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and sends a set mode command to guided.
    /// </summary>
    [Fact]
    public async Task Should_Set_Guided_Mode_Through_SITL_Extended()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        VehicleState? vehicle = null!;

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicles().First();
                Assert.Equal(VehicleConnectionState.Online, vehicle.ConnectionState);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse armResponse = await vehicleService.ArmAsync(vehicle.VehicleId, TestContext.Current.CancellationToken);

        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", armResponse.VehicleId, armResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, armResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        VehicleCommandResponse modeResponse = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);

        logger.LogTrace("Set mode response: Vehicle={VehicleId}, Result={Result}", modeResponse.VehicleId, modeResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
                Assert.Equal(VehicleMode.Guided, vehicle.Mode);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        modeResponse = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.AltHold, TestContext.Current.CancellationToken);

        logger.LogTrace("Set mode response: Vehicle={VehicleId}, Result={Result}", modeResponse.VehicleId, modeResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
                Assert.Equal(VehicleMode.AltHold, vehicle.Mode);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        modeResponse = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Land, TestContext.Current.CancellationToken);

        logger.LogTrace("Set mode response: Vehicle={VehicleId}, Result={Result}", modeResponse.VehicleId, modeResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
                Assert.Equal(VehicleMode.Land, vehicle.Mode);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        modeResponse = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Stabilize, TestContext.Current.CancellationToken);

        logger.LogTrace("Set mode response: Vehicle={VehicleId}, Result={Result}", modeResponse.VehicleId, modeResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
                Assert.Equal(VehicleMode.Stabilize, vehicle.Mode);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);


        modeResponse = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Loiter, TestContext.Current.CancellationToken);

        logger.LogTrace("Set mode response: Vehicle={VehicleId}, Result={Result}", modeResponse.VehicleId, modeResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
                Assert.Equal(VehicleMode.Loiter, vehicle.Mode);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        modeResponse = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Rtl, TestContext.Current.CancellationToken);

        logger.LogTrace("Set mode response: Vehicle={VehicleId}, Result={Result}", modeResponse.VehicleId, modeResponse.Result);

        Assert.Equal(VehicleCommandResult.Accepted, modeResponse.Result);

        await EventuallyAsync(
            () =>
            {
                vehicle = vehicleService.GetVehicleState(vehicle.VehicleId);
                DomainException.ThrowIfNull(vehicle);
                Assert.True(vehicle.IsArmed);
                Assert.Equal(VehicleMode.Rtl, vehicle.Mode);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and sends a set mode command to stabilize.
    /// </summary>
    [Fact]
    public async Task Should_Set_Stabilize_Mode_Through_SITL()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        VehicleState testVehicle = await WaitForRegisteredVehicle();

        VehicleCommandResponse response = await vehicleService.ArmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();
        VehicleState vehicle = vehicles.First();
        vehicle.IsArmed.Should().BeTrue();

        response = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);

        vehicles = vehicleService.GetVehicles();
        vehicle = vehicles.First();
        vehicle.Mode.Should().Be(VehicleMode.Guided);
    }

    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and sends a set mode command to RTL.
    /// </summary>
    [Fact]
    public async Task Should_Set_Rtl_Mode_Through_SITL()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        VehicleState testVehicle = await WaitForRegisteredVehicle();

        VehicleCommandResponse response = await vehicleService.ArmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);
        IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();
        VehicleState vehicle = vehicles.First();
        vehicle.IsArmed.Should().BeTrue();

        response = await vehicleService.SetModeAsync(vehicle.VehicleId, VehicleMode.Guided, TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);

        vehicles = vehicleService.GetVehicles();
        vehicle = vehicles.First();
        vehicle.Mode.Should().Be(VehicleMode.Guided);
    }


    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and verifies that attitude updates are received and processed.
    /// </summary>
    [Fact]
    public async Task Should_Update_Attitude_From_SITL()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        await EventuallyAsync(
            () =>
            {
                VehicleState vehicle = vehicleService.GetVehicles().First();

                Assert.NotNull(vehicle.Roll);
                Assert.NotNull(vehicle.Pitch);
                Assert.NotNull(vehicle.Yaw);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and verifies that position updates are received and processed.
    /// </summary>
    [Fact]
    public async Task Should_Update_Position_From_SITL()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        await EventuallyAsync(
            () =>
            {
                VehicleState vehicle = vehicleService.GetVehicles().First();

                Assert.NotNull(vehicle.Latitude);
                Assert.NotNull(vehicle.Longitude);
                Assert.NotNull(vehicle.Altitude);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Registers a vehicle from the SITL heartbeat message and verifies that battery updates are received and processed.
    /// </summary>
    [Fact]
    public async Task Should_Update_Battery_From_SITL()
    {
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        await EventuallyAsync(
            () =>
            {
                VehicleState vehicle = vehicleService.GetVehicles().First();

                Assert.NotNull(vehicle.BatteryVoltage);
                Assert.NotNull(vehicle.BatteryRemaining);
            },
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///  
    /// </summary>
    [Fact]
    public async Task Should_Receive_StatusText_From_SITL()
    {
        IEventHub eventHub = serviceProvider.GetRequiredService<IEventHub>();
        ILogger<SmokeTestsSitl> logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsSitl>>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        VehicleState testVehicle = await WaitForRegisteredVehicle();

        var completion = new TaskCompletionSource<StatusTextMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable subscription = eventHub.SubscribeAsync<StatusTextMessage>(MavLinkEventTopics.ReceivedMessage,
            (message, cancellationToken) =>
            {
                completion.TrySetResult(message);
                return Task.CompletedTask;
            });


        VehicleCommandResponse response = await vehicleService.ArmAsync(testVehicle.VehicleId, TestContext.Current.CancellationToken);
        logger.LogTrace("Arm response: Vehicle={VehicleId}, Result={Result}", response.VehicleId, response.Result);
        Assert.Equal(VehicleCommandResult.Accepted, response.Result);

        StatusTextMessage statusText = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(statusText.Text));

        IReadOnlyCollection<VehicleStatusText> notifications = vehicleService.GetVehicleNotifications(testVehicle.VehicleId);
        notifications.Count.Should().BeGreaterThanOrEqualTo(1);
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