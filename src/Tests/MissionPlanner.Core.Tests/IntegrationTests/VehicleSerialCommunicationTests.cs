using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Test.Support.Configuration;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Tests.IntegrationTests;

/// <summary>
/// Tests for the MAVLink client and transport implementations.
/// </summary>
public class VehicleSerialCommunicationTests
{
    private readonly ITestOutputHelper output;

    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public VehicleSerialCommunicationTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Establish_Serial_Communication_With_Vehicle()
    {
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var logger = serviceProvider.GetRequiredService<ILogger<VehicleTests>>();
        var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();

        var counter = 0;
        var vehicleRegistered = false;
        using var subscription = eventHub.SubscribeAsync<HeartbeatMessage>(MavLinkEventTopics.ReceivedMessage, (HeartbeatMessage evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received Heartbeat Message {counter} from Vehicle: {VehicleId}", counter, evt.SystemId);
            Assert.True(vehicleRegistered);
            counter++;
            if (counter >= 10)
            {
                ts.TrySetResult();
            }

            return Task.CompletedTask;
        });

        using var _1 = domainEventHub.SubscribeDomainEvent<VehicleRegistered>((VehicleRegistered evt) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
        });

        using var _2 = domainEventHub.SubscribeDomainEvent<VehicleStateUpdated>((VehicleStateUpdated evt) => logger.LogInformation("Test-Received VehicleStateUpdated Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId));
        using var _3 = domainEventHub.SubscribeDomainEvent<VehicleConnectionStateChanged>((VehicleConnectionStateChanged evt) => logger.LogInformation("Test-Received VehicleConnectionStateChanged Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId));

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;

        // Create serial transport
        var transport = domainFactory.Create<ISerialMavLinkTransport, string, int>(portName, baudRate);
        Assert.NotNull(transport);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport, IOptions<TransportEndpoint>, IDateTimeProvider>(transport, transportOptions, dateTimeProvider);
        Assert.NotNull(client);

        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();
        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        Assert.NotNull(connection);

        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.True(vehicleRegistered);
    }


    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Establish_Serial_Communication_With_Vehicle_Using_VehicleConnectionService_And_Retreive_Parameters()
    {
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var logger = serviceProvider.GetRequiredService<ILogger<VehicleTests>>();
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;
        var vehicleRegistered = false;
        await using var vehicleConnectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();

        using var eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>((VehicleConnected evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleConnected Message With Vehicle: {VehicleId}", evt.VehicleId);
            vehicleRegistered = true;
            ts.TrySetResult();
            return Task.CompletedTask;
        });
        var connection = await vehicleConnectionService.ConnectSerialAsync(portName, baudRate, ct);
        DomainException.ThrowIfNull(connection);

        // 3. CRITICAL: Wait for vehicle to be ready
        await Task.Delay(1500, ct); // 1.5 seconds

        //If timeout happens, then the vehicle is not registered due to missing HeartbeatMessage, but it may be connected and receiving AttitudeMessage.
        await ts.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

        Assert.True(vehicleRegistered);
        DomainException.ThrowIfNull(connection.VehicleId);
        DomainException.ThrowIfNull(connection.ConnectionSession);

        var vehicleId = connection.VehicleId.Value;
        var parameterRegistry = serviceProvider.GetRequiredService<IVehicleParameterRegistry>();

        ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await EventuallyAsync(() =>
        {
            var parameterCount = parameterRegistry.GetParameterCount(vehicleId);
            Assert.True(parameterCount.HasValue && parameterCount.Value > 0, "Vehicle parameters should be loaded");

            logger.LogInformation("Test-Received Parameters From Vehicle: {parameterCount}", parameterCount);
            ts.TrySetResult();
        }, TimeSpan.FromSeconds(30), ct);

        var result = await connection.ConnectionSession.ParameterService.RequestParameterListAsync(vehicleId, ct);

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

        Assert.True(result);

        foreach (var vehicleParameter in parameterRegistry.GetAllParameters(vehicleId))
        {
            var parameter = vehicleParameter.Value;
            logger.LogInformation("{ParameterName} = {ParameterValue}", vehicleParameter.Key, vehicleParameter.Value.ToString());
        }
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
