using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Tests.Configuration;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;

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
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Establish_Serial_Communication_With_VehicleAsync()
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


    //eventHub.SubscribeDomainEvent<VehicleArmed>((m) => OnVehicleArmed((VehicleArmed)m));
    //eventHub.SubscribeDomainEvent<VehicleDisarmed>((m) => OnVehicleDisarmed((VehicleDisarmed)m));
    //eventHub.SubscribeDomainEvent<VehicleConnectionStateChanged>((m) => OnVehicleConnectionStateChanged((VehicleConnectionStateChanged)m));
    //eventHub.SubscribeDomainEvent<VehicleModeChanged>((m) => OnVehicleModeChanged((VehicleModeChanged)m));
    //eventHub.SubscribeDomainEvent<VehicleRegistered>((m) => OnVehicleRegistered((VehicleRegistered)m));
    //eventHub.SubscribeDomainEvent<VehicleStateUpdated>((m) => OnVehicleStateUpdated((VehicleStateUpdated)m));
    //eventHub.SubscribeDomainEvent<VehicleStatusMessageReceived>((m) => OnVehicleStatusMessageReceived((VehicleStatusMessageReceived)m))
    //await EventuallyAsync(
    //    () =>
    //    {
    //        //var state = vehicleService.GetVehicleState(vehicleId);

    //        //Assert.Equal(vehicleId, state.VehicleId);
    //        //Assert.False(state.IsArmed);
    //        ts.TrySetResult();
    //    },
    //    TimeSpan.FromSeconds(5),
    //    TestContext.Current.CancellationToken);


    //     await connection.StartAsync(TestContext.Current.CancellationToken);
    //var connectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();

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
