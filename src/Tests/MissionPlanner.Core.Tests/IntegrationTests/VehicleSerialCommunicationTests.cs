using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
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
    public async Task Should_Establish_Serial_Communication_With_Vehicle()
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

        using var _1 = domainEventHub.SubscribeDomainEvent<VehicleRegistered>((VehicleRegistered evt) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message {counter} With Vehicle: {VehicleId}", counter, evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
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
    /// Tests that all Vehicle parameters is received
    /// </summary>
    [Fact]
    public async Task Should_Retrieve_Parameters()
    {
        logger.LogInformation("New Test: Should_Retrieve_Parameters");
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
        var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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


    /// <summary>
    /// Tests that all Vehicle parameters is received
    /// </summary>
    [Fact]
    public async Task Should_Retrieve_Parameters_Using_Streaming()
    {
        logger.LogInformation("New Test: Should_Retrieve_Parameters_Using_Streaming");

        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
        var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
        await using var vehicleConnectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();

        using var eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>((VehicleConnected evt, CancellationToken ct) =>
        {
            logger.LogInformation("Test-Received VehicleConnected Message With Vehicle: {VehicleId}", evt.VehicleId);
            ts.TrySetResult();
            return Task.CompletedTask;
        });

        using var _1 = domainEventHub.SubscribeDomainEvent<VehicleRegistered>((VehicleRegistered evt) =>
        {
            logger.LogInformation("Test-Received VehicleRegistered Message With Vehicle: {VehicleId}", evt.VehicleId);
            Assert.False(vehicleRegistered);
            vehicleRegistered = true;
            tsRegistered.TrySetResult();
        });

        var connection = await vehicleConnectionService.ConnectSerialAsync(portName, baudRate, ct);
        DomainException.ThrowIfNull(connection);

        // 3. CRITICAL: Wait for vehicle to be ready
        await Task.Delay(1500, ct); // 1.5 seconds

        //If timeout happens, then the vehicle is not registered due to missing HeartbeatMessage, but it may be connected and receiving AttitudeMessage.
        await tsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

        ct = new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token;

        Assert.True(vehicleRegistered);
        DomainException.ThrowIfNull(connection.VehicleId);
        DomainException.ThrowIfNull(connection.ConnectionSession);

        var vehicleId = connection.VehicleId.Value;

        CancellationTokenSource ctsProgress = new();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(120));

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
        var result = await vehicleParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: ctsProgress.Token);

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

    [Fact]
    public async Task Diagnose_Parameter_Reading()
    {
        logger.LogInformation("New Test: Diagnose_Parameter_Reading");
        var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serialPortDiscoveryService = serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        var availablePorts = serialPortDiscoveryService.GetAvailablePorts();
        Assert.True(availablePorts.Any(), "Connect a ArduPilot Vehicle");

        var encoder = serviceProvider.GetRequiredService<IMavLinkParameterEncoder>();

        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = availablePorts.First();
        var baudRate = 115200;

        var linkedCts = new CancellationTokenSource();
        var transport = domainFactory.Create<ISerialMavLinkTransport, string, int>(portName, baudRate);
        var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport>(transport);
        await transport.ConnectAsync(TestContext.Current.CancellationToken);

        var vehicleId = await WaitForVehicleHeartbeatAsync(client, transport, linkedCts.Token);

        //Wait for vehicle to be ready
        await Task.Delay(1500, linkedCts.Token); // 1.5 seconds

        DomainException.ThrowIfNull(vehicleId);

        _ = Task.Run(async () => await ReceiveLoopAsync(async (MavLinkDataReceived received, CancellationToken c) =>
        {
            logger.LogDebug("Test - Received MAVLink data: {Data}", received);
            await OnDataReceivedAsync(received, c);
        }, transport, dateTimeProvider, linkedCts.Token), linkedCts.Token);


        var packet = encoder.EncodeParamRequestList(vehicleId.Value.SystemId, vehicleId.Value.ComponentId);

        var endpoint = new TransportEndPoint("mavlink", "unknown", 0);

        await client.SendAsync(packet, endpoint, linkedCts.Token);

        logger.LogInformation("📤 Test-Sent PARAM_REQUEST_LIST to {VehicleId}", vehicleId);

        await Task.Delay(TimeSpan.FromSeconds(30), linkedCts.Token);
    }

    private async Task OnDataReceivedAsync(MavLinkDataReceived received, CancellationToken cancellationToken)
    {
        var parsedFrames = frameParser.Parse(received.Data.Span, received.RemoteEndpoint, received.ReceivedAt);
        foreach (var frame in parsedFrames)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (messageDecoder.TryDecode(frame, out var message) && message is not null)
            {
                // MessageId=22 as ParamValueMessage
                if (frame.MessageId == 22 && message is ParamValueMessage paramValueMessage)
                {
                    logger.LogInformation("Test-MavLinkConnection - Received PARAM_VALUE: {ParamId} = {ParamValue} (Index {ParamIndex}/{ParamCount})", paramValueMessage.ParamId, paramValueMessage.ParamValue, paramValueMessage.ParamIndex, paramValueMessage.ParamCount);
                }
            }
            else
            {
                logger.LogError("Test-MavLinkConnection - Failed to decode message from frame {frame}", frame.MessageId);
            }
        }
    }

    private async Task ReceiveLoopAsync(Func<MavLinkDataReceived, CancellationToken, Task>? dataReceived, IMavLinkTransport transport, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        var receiveBufferSize = 512;
        var buffer = new byte[receiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested && transport.IsConnected)
            {
                var result = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.BytesRead <= 0)
                {
                    continue;
                }

                //logger.LogTrace("MavLinkClient - Received {BytesRead} bytes from MAVLink transport.", result.BytesRead);
                var copy = new byte[result.BytesRead];
                buffer.AsMemory(0, result.BytesRead).CopyTo(copy);

                var received = new MavLinkDataReceived(copy, result.RemoteEndpoint, dateTimeProvider.UtcNow);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var handler = dataReceived;

                if (handler is not null)
                {
                    await handler(received, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (ObjectDisposedException)
        {
            // Normal when transport is closed while blocked in ReadAsync().
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MavLinkClient - Unexpected exception in ReceiveLoop.");
            throw;
        }
        finally
        {
            await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogTrace("MavLinkClient - MAVLink client stopped.");
        }
    }

    private async Task<HeartbeatMessage?> ReceiveHeartbeatLoopAsync(IMavLinkTransport transport, CancellationToken cancellationToken)
    {
        var receiveBufferSize = 512;
        var buffer = new byte[receiveBufferSize];
        var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && transport.IsConnected)
            {
                var result = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.BytesRead <= 0)
                {
                    continue;
                }

                //logger.LogTrace("Test - MavLinkClient - Waiting for Heartbeat -  Received {BytesRead} bytes from MAVLink transport.", result.BytesRead);
                var copy = new byte[result.BytesRead];
                buffer.AsMemory(0, result.BytesRead).CopyTo(copy);

                var received = new MavLinkDataReceived(copy, result.RemoteEndpoint, dateTimeProvider.UtcNow);
                var parsedFrames = frameParser.Parse(received.Data.Span, received.RemoteEndpoint, received.ReceivedAt);
                foreach (var frame in parsedFrames)
                {
                    //logger.LogTrace("Test - MavLinkClient - Waiting for Heartbeat - MavLinkConnection - Processing frame {frame}", frame.MessageId);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (messageDecoder.TryDecode(frame, out var message) && message is not null)
                    {
                        logger.LogTrace("Test - MavLinkClient - Waiting for Heartbeat - MavLinkConnection - Writing Decoded Message { MessageType}  {frame}", message.GetType().Name, frame.MessageId);

                        if (message is HeartbeatMessage heartbeat)
                        {
                            return heartbeat;
                        }
                    }
                    else
                    {
                        logger.LogError(" Test-  MavLinkClient - Waiting for Heartbeat - MavLinkConnection - Failed to decode message from frame {frame}", frame.MessageId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (ObjectDisposedException)
        {
            // Normal when transport is closed while blocked in ReadAsync().
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MavLinkClient - Unexpected exception in ReceiveLoop.");
            throw;
        }
        finally
        {
            //await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogTrace("MavLinkClient - MAVLink client stopped.");
        }

        return null;
    }

    private async Task<VehicleId?> WaitForVehicleHeartbeatAsync(IMavLinkClient client, IMavLinkTransport transport, CancellationToken cancellationToken)
    {
        try
        {
            var heartbeat = await ReceiveHeartbeatLoopAsync(transport, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return new VehicleId(0, 0);
            }

            if (heartbeat is not null)
            {
                return new VehicleId(heartbeat.SystemId, heartbeat.ComponentId);
            }

            logger.LogWarning(" Test - Timeout waiting for vehicle heartbeat");
            return new VehicleId(0, 0);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Test - Cancelled while waiting for vehicle heartbeat");
            return null;
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
