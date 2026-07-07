using System.Buffers.Binary;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Simulator;
using MissionPlanner.Test.Support.Configuration;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests for the MAVLink client and transport implementations.
/// </summary>
public class VehicleTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;
    private readonly IPEndPoint simulatorIPEndPoint;
    private readonly IPEndPoint targetIPEndPoint;
    private readonly int port;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public VehicleTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();

        var logger = serviceProvider.GetRequiredService<ILogger<VehicleTests>>();
        var endPoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>().Value;

        logger.LogInformation($"Test configuration initialized. UDP local:  {endPoint.LocalHost}:{endPoint.LocalPort}");
        logger.LogInformation($"Test configuration initialized. UDP remote: {endPoint.RemoteHost}:{endPoint.RemotePort}");
        //Test configuration initialized.UDP local: 0.0.0.0:14550
        //Test configuration initialized.UDP remote: 127.0.0.1:14551

        var targetPort = endPoint.LocalPort;
        var targetIp = endPoint.RemoteHost;

        var targetAddress = string.IsNullOrWhiteSpace(endPoint.RemoteHost)
            ? IPAddress.Any
            : IPAddress.Parse(targetIp);
        port = endPoint.RemotePort;
        simulatorIPEndPoint = new IPEndPoint(targetAddress, targetPort);

        targetIPEndPoint = new IPEndPoint(IPAddress.Parse(targetIp), port);
    }

    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_When_Heartbeat_Is_ReceivedAsync()
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var handler = domainFactory.Create<IHeartbeatVehicleHandler, IVehicleRegistry>(registry);
        var heartbeat = new HeartbeatMessage(
            1,
            1,
            simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        await handler.Handle(heartbeat, TestContext.Current.CancellationToken);
        var vehicle = registry.GetRequired(new VehicleId(1, 1));

        Assert.Equal(new VehicleId(1, 1), vehicle.Id);
        Assert.Single(registry.Vehicles);
        Assert.Equal(VehicleConnectionState.Online, vehicle.State.ConnectionState);
        Assert.Equal(2, vehicle.State.VehicleType);
        Assert.Equal(3, vehicle.State.Autopilot);
    }

    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received from the MAVLink connection.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_From_Received_Heartbeat_MessageAsync()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var handler = serviceProvider.GetRequiredService<IHeartbeatVehicleHandler>();
        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        output.WriteLine($"Client IsRunning: {client.IsRunning}");
        output.WriteLine($"Transport IsConnected: {client.IsConnected}");
        await using var simulator = new FakeMavLinkVehicle2(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
            //"127.0.0.1", 14550, 14551,
            simulatorIPEndPoint.Address.ToString(),
            simulatorIPEndPoint.Port,
            port,
            TimeSpan.FromMilliseconds(100));


        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HeartbeatMessage? messageResult = null;
        using var subscription = eventHub.SubscribeAsync<HeartbeatMessage>(MavLinkEventTopics.ReceivedMessage, (heartbeatMessage, ct) =>
        {
            messageResult = heartbeatMessage;
            ts.TrySetResult();
            return Task.CompletedTask;
        });


        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => simulator.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);


        await ts.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        messageResult.Should().NotBeNull();
        var message = messageResult!;


        await handler.Handle(message, TestContext.Current.CancellationToken);
        var vehicle = registry.GetRequired(new VehicleId(1, 1))!;

        Assert.Equal(new VehicleId(1, 1), vehicle.Id);
        Assert.Single(registry.Vehicles);
        Assert.Equal(VehicleConnectionState.Online, vehicle.State.ConnectionState);
    }


    /// <summary>
    /// Tests that an existing vehicle is updated when a repeated heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Update_Existing_Vehicle_When_Heartbeat_Is_RepeatedAsync()
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var handler = domainFactory.Create<IHeartbeatVehicleHandler, IVehicleRegistry>(registry);

        var first = new HeartbeatMessage(
            1, 1, simulatorIPEndPoint.ToTransportEndPoint("udp"), 0, 2, 3, 0, 4, 3,
            DateTimeOffset.UtcNow);

        var second = new HeartbeatMessage(
            1, 1, simulatorIPEndPoint.ToTransportEndPoint("udp"), 42, 2, 3, 81, 4, 3,
            DateTimeOffset.UtcNow.AddSeconds(1));

        await handler.Handle(first, TestContext.Current.CancellationToken);
        await handler.Handle(second, TestContext.Current.CancellationToken);
        var vehicles = registry.Vehicles;
        var vehicle1 = registry.GetRequired(vehicles.First().Id)!;
        var vehicle2 = registry.GetRequired(vehicles.Last().Id)!;

        Assert.Same(vehicle1, vehicle2);
        Assert.Single(registry.Vehicles);
        Assert.Equal(42u, vehicle2.State.CustomMode);
        Assert.Equal(81, vehicle2.State.BaseMode);
    }

    /// <summary>
    /// Tests that a vehicle is registered when the message pump receives a heartbeat message.
    /// </summary>
    [Fact]
    public async Task Should_Register_Vehicle_When_Message_Pump_Receives_Heartbeat()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();

        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await using var simulator = new FakeMavLinkVehicle2(
            serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
            serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
            simulatorIPEndPoint.Address.ToString(),
            simulatorIPEndPoint.Port,
            port,
            TimeSpan.FromMilliseconds(100));

        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => simulator.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await EventuallyAsync(() => Assert.Single(registry.Vehicles), TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        Assert.Contains(registry.Vehicles, vehicle => vehicle.Id == new VehicleId(1, 1));
    }

    /// <summary>
    /// Tests that a vehicle's position is updated when a GlobalPositionInt message is received.
    /// </summary>
    [Fact]
    public async Task Should_Update_Vehicle_Position_From_GlobalPositionInt_MessageAsync()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();

        var vehicleId = new VehicleId(1, 1);

        registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        var handler = domainFactory.Create<IPositionVehicleHandler, IVehicleRegistry>(registry);

        await handler.Handle(
            new GlobalPositionIntMessage(
                1,
                1, simulatorIPEndPoint.ToTransportEndPoint("udp"),
                56.1629,
                10.2039,
                12.5,
                DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var vehicle = registry.GetRequired(vehicleId);

        Assert.Equal(56.1629, vehicle.State.Latitude);
        Assert.Equal(10.2039, vehicle.State.Longitude);
        Assert.Equal(12.5, vehicle.State.Altitude);
    }

    /// <summary>
    /// Tests that a vehicle's battery is updated when a SysStatusMessage is received.
    /// </summary>
    [Fact]
    public async Task Should_Update_Battery_From_SysStatusMessage_MessageAsync()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();

        var vehicleId = new VehicleId(1, 1);

        registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        var handler = domainFactory.Create<IBatteryVehicleHandler, IVehicleRegistry>(registry);

        await handler.Handle(
            new SysStatusMessage(
                1,
                1, simulatorIPEndPoint.ToTransportEndPoint("udp"),
                56,
                (float)10.0,
                DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var vehicle = registry.GetRequired(vehicleId);

        Assert.Equal(56, vehicle.State.BatteryRemaining);
        Assert.Equal((float)10.0, vehicle.State.BatteryVoltage);
    }

    /// <summary>
    /// Tests that a vehicle's attitude is updated when an AttitudeMessage is received.
    /// </summary>
    [Fact]
    public async Task Should_Update_Attitude_From_AttitudeMessageAsync()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();

        var vehicleId = new VehicleId(1, 1);

        registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        var handler = domainFactory.Create<IAttitudeVehicleHandler, IVehicleRegistry>(registry);

        await handler.Handle(
            new AttitudeMessage(
                1,
                1, simulatorIPEndPoint.ToTransportEndPoint("udp"),
                56.1629,
                10.2039,
                12.5,
                DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var vehicle = registry.GetRequired(vehicleId);

        Assert.Equal(56.1629, vehicle.State.Roll);
        Assert.Equal(10.2039, vehicle.State.Pitch);
        Assert.Equal(12.5, vehicle.State.Yaw);
    }


    /// <summary>
    /// Tests that a vehicle is marked as stale when its heartbeat is old.
    /// </summary>
    [Fact]
    public void Should_Mark_Vehicle_As_Stale_When_Heartbeat_Is_Old()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();

        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();

        var receivedAt = DateTimeOffset.UtcNow;

        var vehicleRegistryResult = registry.RegisterOrUpdateHeartbeat(
            new VehicleId(1, 1), simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt);

        registry.UpdateConnectionStates(receivedAt.AddSeconds(3), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        Assert.Equal(VehicleConnectionState.Stale,
            vehicleRegistryResult.Vehicle.State.ConnectionState);
    }

    /// <summary>
    /// Tests that a vehicle is marked as degraded when its heartbeat is very old.
    /// </summary>
    [Fact]
    public void Should_Mark_Vehicle_As_Degraded_When_Heartbeat_Is_Old()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();

        var receivedAt = DateTimeOffset.UtcNow;

        var vehicleRegistryResult = registry.RegisterOrUpdateHeartbeat(
            new VehicleId(1, 1), simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt);

        registry.UpdateConnectionStates(receivedAt.AddSeconds(6), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        Assert.Equal(VehicleConnectionState.Degraded, vehicleRegistryResult.Vehicle.State.ConnectionState);
    }

    /// <summary>
    /// Tests that a vehicle is marked as offline when its heartbeat is very old.
    /// </summary>
    [Fact]
    public void Should_Mark_Vehicle_As_Offline_When_Heartbeat_Is_Very_Old()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();

        var receivedAt = DateTimeOffset.UtcNow;

        var vehicleRegistryResult = registry.RegisterOrUpdateHeartbeat(
            new VehicleId(1, 1), simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt);

        registry.UpdateConnectionStates(receivedAt.AddSeconds(12), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        Assert.Equal(VehicleConnectionState.Offline, vehicleRegistryResult.Vehicle.State.ConnectionState);
    }


    /// <summary>
    /// Tests that a vehicle is marked as online when a new heartbeat arrives after it was marked degraded.
    /// </summary>
    [Fact]
    public void Should_Mark_Vehicle_Online_When_New_Heartbeat_Arrives_After_Degraded()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();


        var receivedAt = DateTimeOffset.UtcNow;
        var vehicleId = new VehicleId(1, 1);

        var vehicleRegistryResult = registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt);

        registry.UpdateConnectionStates(receivedAt.AddSeconds(6), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        Assert.Equal(VehicleConnectionState.Degraded, vehicleRegistryResult.Vehicle.State.ConnectionState);

        registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt.AddSeconds(7));

        Assert.Equal(
            VehicleConnectionState.Online,
            vehicleRegistryResult.Vehicle.State.ConnectionState);
    }

    /// <summary>
    /// Tests that a vehicle is marked as online when a new heartbeat arrives after it was marked offline.
    /// </summary>
    [Fact]
    public void Should_Mark_Vehicle_Online_When_New_Heartbeat_Arrives_After_Offline()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();


        var receivedAt = DateTimeOffset.UtcNow;
        var vehicleId = new VehicleId(1, 1);

        var vehicleRegistryResult = registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt);

        registry.UpdateConnectionStates(receivedAt.AddSeconds(12), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        Assert.Equal(
            VehicleConnectionState.Offline, vehicleRegistryResult.Vehicle.State.ConnectionState);

        registry.RegisterOrUpdateHeartbeat(
            vehicleId, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            0,
            4,
            3,
            receivedAt.AddSeconds(7));

        Assert.Equal(
            VehicleConnectionState.Online,
            vehicleRegistryResult.Vehicle.State.ConnectionState);
    }

    /// <summary>
    /// Tests that an arm command is correctly encoded into a MAVLink CommandLong frame. 
    /// </summary>
    [Fact]
    public void Should_Encode_Arm_CommandLong_Frame()
    {
        var encoder = serviceProvider.GetRequiredService<IMavLinkCommandEncoder>();

        var packet = encoder.EncodeArmDisarm(
            1,
            1,
            true);

        Assert.Equal(0xFD, packet[0]);
        Assert.Equal(33, packet[1]);

        var messageId =
            packet[7]
            | ((uint)packet[8] << 8)
            | ((uint)packet[9] << 16);

        Assert.Equal(MessageIds.CommandLong, messageId);

        var payload = packet.AsSpan(10, 33);

        var param1 = BitConverter.ToSingle(payload[0..4]);
        var command = BinaryPrimitives.ReadUInt16LittleEndian(payload[28..30]);

        Assert.Equal(1.0f, param1);
        Assert.Equal(MavLinkCommandIds.ComponentArmDisarm, command);
        Assert.Equal(1, payload[30]); // target system
        Assert.Equal(1, payload[31]); // target component
    }

    /// <summary>
    /// Tests that a CommandAck message is correctly decoded from a MAVLink frame. 
    /// </summary>
    [Fact]
    public void Should_Decode_CommandAck_Message()
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var payload = new byte[]
        {
            0x90, 0x01, // 400 COMPONENT_ARM_DISARM
            0x00 // ACCEPTED
        };

        var frame = new MavLinkFrame(
            1,
            1,
            simulatorIPEndPoint.ToTransportEndPoint("udp"),
            MessageIds.CommandAck,
            0,
            payload,
            new ReadOnlyMemory<byte>(),
            receivedAt);

        var decoder = new CommandAckMessageDecoder();

        var decoded = decoder.TryDecode(frame, out var message);

        Assert.True(decoded);

        var ack = Assert.IsType<CommandAckMessage>(message);

        Assert.Equal(1, ack.SystemId);
        Assert.Equal(1, ack.ComponentId);
        Assert.Equal(400, ack.Command);
        Assert.Equal(0, ack.Result);
    }

    /// <summary>
    /// Tests that sending an arm command results in receiving a command acknowledgment from the simulator. 
    /// </summary>
    [Fact]
    public async Task Should_Receive_CommandAck_When_Arm_Command_Is_Sent()
    {
        var options = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>();
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();

        await using var client = serviceProvider.GetRequiredService<IMavLinkClient>();
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        await using var simulator =
            new FakeMavLinkVehicle2(
                serviceProvider.GetRequiredService<IMavLinkFrameParser>(),
                serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>(),
                simulatorIPEndPoint.Address.ToString(),
                simulatorIPEndPoint.Port,
                port,
                TimeSpan.FromMilliseconds(100));


        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CommandAckMessage? messageResult = null;
        using var subscription = eventHub.SubscribeAsync<CommandAckMessage>(MavLinkEventTopics.ReceivedMessage, (commandAckMessage, ct) =>
        {
            messageResult = commandAckMessage;
            ts.TrySetResult();
            return Task.CompletedTask;
        });

        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => simulator.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        var encoder = serviceProvider.GetRequiredService<IMavLinkCommandEncoder>();
        var armCommand = encoder.EncodeArmDisarm(1, 1, true);
        await connection.SendRawAsync(armCommand, targetIPEndPoint.ToEndPoint(), TestContext.Current.CancellationToken);


        await ts.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        messageResult.Should().NotBeNull();
        var message = messageResult!;

        Assert.Equal(1, message.SystemId);
        Assert.Equal(1, message.ComponentId);
        Assert.Equal(MavLinkCommandIds.ComponentArmDisarm, message.Command);
        Assert.Equal(0, message.Result);
    }

    /// <summary>
    /// Tests that the armed state is correctly updated from the heartbeat base mode.
    /// </summary>
    [Fact]
    public async Task Should_Update_Armed_State_From_Heartbeat_BaseModeAsync()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var handler = serviceProvider.GetRequiredService<IHeartbeatVehicleHandler>();
        var heartbeat = new HeartbeatMessage(
            1, 1, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            0,
            2,
            3,
            128,
            4,
            3,
            DateTimeOffset.UtcNow);

        await handler.Handle(heartbeat, TestContext.Current.CancellationToken);
        var vehicle = registry.GetRequired(new VehicleId(1, 1))!;

        Assert.True(vehicle.State.IsArmed);
    }

    /// <summary>
    /// Tests that the vehicle mode is correctly updated from the heartbeat custom mode. 
    /// </summary>
    [Fact]
    public async Task Should_Update_Mode_From_Heartbeat_CustomModeAsync()
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var handler = domainFactory.Create<IHeartbeatVehicleHandler, IVehicleRegistry>(registry);

        var heartbeat = new HeartbeatMessage(
            1, 1, simulatorIPEndPoint.ToTransportEndPoint("udp"),
            4,
            2,
            3,
            0,
            4,
            3,
            DateTimeOffset.UtcNow);

        await handler.Handle(heartbeat, TestContext.Current.CancellationToken);
        var vehicle = registry.GetRequired(new VehicleId(1, 1))!;

        Assert.Equal(VehicleMode.Guided, vehicle.State.Mode);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Should_Update_Position()
    {
        var vehicle = CreateVehicleSession();

        vehicle.ApplyPosition(
            56.1629,
            10.2039,
            12.5);

        Assert.Equal(56.1629, vehicle.State.Latitude);
        Assert.Equal(10.2039, vehicle.State.Longitude);
        Assert.Equal(12.5, vehicle.State.Altitude);
    }


    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Should_Update_Attitude()
    {
        var vehicle = CreateVehicleSession();

        vehicle.ApplyAttitude(0.1, -0.2, 1.5);

        Assert.Equal(0.1, vehicle.State.Roll);
        Assert.Equal(-0.2, vehicle.State.Pitch);
        Assert.Equal(1.5, vehicle.State.Yaw);
    }


    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Should_Update_Battery()
    {
        var vehicle = CreateVehicleSession();

        vehicle.ApplyBattery(87, 11.4f);

        Assert.Equal(87, vehicle.State.BatteryRemaining);
        Assert.Equal(11.4f, vehicle.State.BatteryVoltage);
    }


    private VehicleSession CreateVehicleSession()
    {
        var state = new VehicleState(
            new VehicleId(1, 1),
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            DateTimeOffset.UtcNow,
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

        return new VehicleSession(state, simulatorIPEndPoint.ToEndPoint());
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
}
