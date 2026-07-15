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
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
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

    private readonly ILogger logger;
    private readonly IMavLinkFrameParser frameParser;
    private readonly IMavLinkMessageDecodeHandler messageDecoder;

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
        logger = serviceProvider.GetRequiredService<ILogger<VehicleSerialCommunicationTests>>();
        frameParser = serviceProvider.GetRequiredService<IMavLinkFrameParser>();
        messageDecoder = serviceProvider.GetRequiredService<IMavLinkMessageDecodeHandler>();
    }

    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Establish_LowLevel_Serial_Communication_With_Vehicle()
    {
        logger.LogInformation("New Test: Should_Establish_Serial_Communication_With_Vehicle");
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource tsRegistered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();

        var counter = 0;
        var vehicleRegistered = false;
        using var subscription = eventHub.SubscribeAsync<HeartbeatMessage>(MavLinkEventTopics.NewMessage, (HeartbeatMessage evt, CancellationToken ct) =>
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

        using var _1 = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>(async (VehicleRegistered evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
        });

        using var _2 = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(async (VehicleStateUpdated evt, CancellationToken ct) => logger.LogInformation("Test-Received VehicleStateUpdated Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId));
        using var _3 = domainEventHub.SubscribeDomainEventAsync<VehicleConnectionStateChanged>(async (VehicleConnectionStateChanged evt, CancellationToken ct) => logger.LogInformation("Test-Received VehicleConnectionStateChanged Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId));

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;

        // Create serial transport
        var transport = domainFactory.Create<ISerialMavLinkTransport, string, int>(portName, baudRate);
        Assert.NotNull(transport);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport>(transport);
        Assert.NotNull(client);

        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();
        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        Assert.NotNull(connection);

        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => connection.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await tsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        //Assert.True(counter > 0);
        Assert.True(vehicleRegistered);
    }

    /// <summary>
    /// Tests that a vehicle is registered when a heartbeat message is received.
    /// </summary>
    [Fact]
    public async Task Should_Start_Complete_Domain_Vehicle_Service_Setup()
    {
        logger.LogInformation("New Test: Should_Start_Complete_Domain_Vehicle_Service_Setup");
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource tsRegistered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();
        var vehicleRegistered = false;
        using var _1 = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>(async (VehicleRegistered evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message With Vehicle: {VehicleId}", evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
        });

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;

        //This entry is enough to establish a connection, start all pumps,  and receive HeartbeatMessage and VehicleRegistered
        var connectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();
        await connectionService.ConnectSerialAsync(portName, baudRate, linkedCts.Token);

        await tsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);

        Assert.True(vehicleRegistered);
    }


    /// <summary>
    /// Tests that all Vehicle parameters is received
    /// </summary>
    [Fact]
    public async Task Should_Handle_Connected_And_Registered()
    {
        logger.LogInformation("New Test: Should_Handle_Connected_And_Registered");
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        var tsConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");

        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;
        var vehicleConnected = false;

        using var _0 = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>((VehicleConnected evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleConnected Message With Vehicle: {VehicleId}", evt.VehicleId);
            vehicleConnected = true;
            tsConnected.TrySetResult();
            return Task.CompletedTask;
        });

        var vehicleRegistered = false;
        using var _1 = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>(async (VehicleRegistered evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message With Vehicle: {VehicleId}", evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
        });

        await using var vehicleConnectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();
        var connectionService = await vehicleConnectionService.ConnectSerialAsync(portName, baudRate, linkedCts.Token);
        DomainException.ThrowIfNull(connectionService);

        // 3. CRITICAL: Wait for vehicle to be ready
        await Task.Delay(1500, linkedCts.Token); // 1.5 seconds

        //If timeout happens, then the vehicle is not registered due to missing HeartbeatMessage, but it may be connected and receiving AttitudeMessage.
        await tsConnected.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);
        await tsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);

        Assert.True(vehicleConnected);
        Assert.True(vehicleRegistered);
        DomainException.ThrowIfNull(connectionService.VehicleId);
        DomainException.ThrowIfNull(connectionService.VehicleId.Value);
        DomainException.ThrowIfNull(connectionService.ConnectionSession);
    }


    /// <summary>
    /// Tests that all Vehicle parameters is received
    /// </summary>
    [Fact]
    public async Task Should_Retrieve_Parameters()
    {
        logger.LogInformation("New Test: Should_Retrieve_Parameters");
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        var tsConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");

        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;
        var vehicleConnected = false;

        using var _0 = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>((VehicleConnected evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleConnected Message With Vehicle: {VehicleId}", evt.VehicleId);
            vehicleConnected = true;
            tsConnected.TrySetResult();
            return Task.CompletedTask;
        });

        var vehicleRegistered = false;
        using var _1 = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>(async (VehicleRegistered evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message With Vehicle: {VehicleId}", evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
        });


        await using var vehicleConnectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();
        var connectionService = await vehicleConnectionService.ConnectSerialAsync(portName, baudRate, linkedCts.Token);
        DomainException.ThrowIfNull(connectionService);

        // 3. CRITICAL: Wait for vehicle to be ready
        await Task.Delay(1500, linkedCts.Token); // 1.5 seconds

        //If timeout happens, then the vehicle is not registered due to missing HeartbeatMessage, but it may be connected and receiving AttitudeMessage.
        await tsConnected.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);
        await tsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);

        Assert.True(vehicleConnected);
        Assert.True(vehicleRegistered);
        DomainException.ThrowIfNull(connectionService.VehicleId);
        DomainException.ThrowIfNull(connectionService.ConnectionSession);

        var vehicleId = connectionService.VehicleId.Value;
        var parameterRegistry = serviceProvider.GetRequiredService<IVehicleParameterRegistry>();

        var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await EventuallyAsync(() =>
        {
            var parameterCount = parameterRegistry.GetParameterCount(vehicleId);
            Assert.True(parameterCount is > 0, "Vehicle parameters should be loaded");

            logger.LogInformation("Test-Received Parameters From Vehicle: {parameterCount}", parameterCount);
            ts.TrySetResult();
        }, TimeSpan.FromSeconds(30), linkedCts.Token);

        var result = await connectionService.ConnectionSession.ParameterService.RequestParameterListAsync(vehicleId, linkedCts.Token);

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);

        Assert.True(result);

        foreach (var vehicleParameter in parameterRegistry.GetAllParameters(vehicleId))
        {
            var parameter = vehicleParameter.Value;
            logger.LogInformation("{ParameterName} = {ParameterValue}", vehicleParameter.Key, vehicleParameter.Value.ToString());
        }
    }

    /// <summary>
    /// Tests that all Vehicle parameters is received
    /// </summary>
    [Fact]
    public async Task Should_Retrieve_Parameters_Using_Streaming()
    {
        logger.LogInformation("New Test: Should_Retrieve_Parameters_Using_Streaming");

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
        var tsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");

        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var domainEventHub = serviceProvider.GetRequiredService<IDomainEventHub>();

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;
        var vehicleRegistered = false;

        using var _1 = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>(async (VehicleRegistered evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message With Vehicle: {VehicleId}", evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
        });

        await using var vehicleConnectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();
        var connectionService = await vehicleConnectionService.ConnectSerialAsync(portName, baudRate, linkedCts.Token);
        DomainException.ThrowIfNull(connectionService);
        // 3. CRITICAL: Wait for vehicle to be ready
        await Task.Delay(1500, linkedCts.Token); // 1.5 seconds

        //If timeout happens, then the vehicle is not registered due to missing HeartbeatMessage, but it may be connected and receiving AttitudeMessage.
        await tsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30), linkedCts.Token);
        Assert.True(vehicleRegistered);

        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        var vehicle = vehicleService.GetVehicles().Single();
        DomainException.ThrowIfNull(vehicle.VehicleId);
        DomainException.ThrowIfNull(vehicle.ConnectionState);
        var vehicleId = vehicle.VehicleId;


        var session = serviceProvider.GetRequiredService<IVehicleConnectionSession>();
        var vehicleParameterStreamService = session.ParameterStreamService;
        IList<VehicleParameter> parameters = [];
        var totalCount = 0;

        IProgress<ParameterStreamProgress>? progress = new Progress<ParameterStreamProgress>(p =>
        {
            totalCount = p.TotalCount;
            var progressCounter = (double)p.ReceivedCount / p.TotalCount;
            var progressMessage = $"Loading parameters... {p.ReceivedCount}/{p.TotalCount}";
            logger.LogDebug("{msg} {count}", progressMessage, progressCounter);
        });
        var result = await vehicleParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: linkedCts.Token);

        if (result.Success)
        {
            foreach (var parameter in result.Parameters.Values)
            {
                parameters.Add(parameter);
            }
        }
        else
        {
            throw new DomainException("Failed to retrieve parameters before Timeout");
        }

        Assert.Equal(totalCount, parameters.Count);
        var parameterRegistry = serviceProvider.GetRequiredService<IVehicleParameterRegistry>();
        var allParameters = parameterRegistry.GetAllParameters(vehicleId);

        Assert.Equal(totalCount, allParameters.Count);

        foreach (var vehicleParameter in allParameters)
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
