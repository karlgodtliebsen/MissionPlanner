using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Simulator.SmokeTests;
using MissionPlanner.Smoke.Simulator.Tests.Configuration;
using MissionPlanner.Transport;

namespace MissionPlanner.Smoke.Simulator.Tests.SmokeTests;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class SmokeTestsDroneBridge
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;
    private readonly IPEndPoint ipEndPoint;
    private readonly IList<TransportEndpoint> endPoints = [];


    /// <summary>
    /// Initializes a new instance of the <see cref="SmokeTestsDroneBridge"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public SmokeTestsDroneBridge(ITestOutputHelper output)
    {
        this.output = output;

        var services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
        var logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsDroneBridge>>();
        var endPoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>().Value;
        logger.LogInformation($"Test configuration initialized. UDP local:  {endPoint.LocalHost}:{endPoint.LocalPort}");
        logger.LogInformation($"Test configuration initialized. UDP remote: {endPoint.RemoteHost}:{endPoint.RemotePort}");
        //Test configuration initialized.UDP local: 0.0.0.0:14550
        //Test configuration initialized.UDP remote: 127.0.0.1:14551

        var targetIp = endPoint.RemoteHost;

        var targetAddress = string.IsNullOrWhiteSpace(targetIp)
            ? IPAddress.Any
            : IPAddress.Parse(targetIp);
        ipEndPoint = new IPEndPoint(targetAddress, endPoint.RemotePort);


        var localPort = 14550;
        var remotePort = 14551;
        var localHost = "0.0.0.0";

        var remoteHost1 = "192.168.1.248";
        var remoteHost2 = "192.168.1.217";

        var endPoint1 = new TransportEndpoint("udp", remotePort, remoteHost1, localPort, localHost, 512);
        var endPoint2 = new TransportEndpoint("udp", remotePort, remoteHost2, localPort, localHost, 512);

        // endPoints = [endPoint1, endPoint2];
        endPoints = [endPoint2];
        foreach (var ep in endPoints)
        {
            logger.LogInformation($"Test configuration initialized. UDP remote: {ep.RemoteHost}:{ep.RemotePort}");
        }
    }


    /// <summary>
    /// Sends a TCP  probe to the DroneBridge without expecting any response.
    /// </summary>
    [Fact]
    public async Task Should_Send_Tcp_Probe_To_DroneBridge_Without_Error()
    {
        foreach (var ep in endPoints)
        {
            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(ep.RemoteHost, 5760, TestContext.Current.CancellationToken);

            Assert.True(tcpClient.Connected);
        }
    }


    /// <summary>
    /// Sends a UDP probe to the DroneBridge without expecting any response.
    /// </summary>
    [Fact]
    public async Task Should_Send_Udp_Probe_To_DroneBridge_Without_Error()
    {
        var smokeTest =
            serviceProvider.GetRequiredService<ITransportSmokeTestService>();

        var payload = TransportProbePayloads.CreateAsciiProbe();

        await smokeTest.SendProbeAsync(payload, ipEndPoint, TestContext.Current.CancellationToken);

        Assert.True(true);
    }

    /// <summary>
    /// Sends a UDP probe to the DroneBridge and verifies that data is received.
    /// </summary>
    [Fact]
    public async Task Should_Send_Udp_Probe_To_DroneBridge()
    {
        var smokeTest = serviceProvider.GetRequiredService<ITransportSmokeTestService>();

        var payload = TransportProbePayloads.CreateAsciiProbe();
        foreach (var endPoint in endPoints)
        {
            var targetAddress = string.IsNullOrWhiteSpace(endPoint.RemoteHost)
                ? IPAddress.Any
                : IPAddress.Parse(endPoint.RemoteHost);

            var targetEndPoint = new IPEndPoint(targetAddress, endPoint.RemotePort);

            await smokeTest.SendProbeAsync(payload, targetEndPoint, TestContext.Current.CancellationToken);

            // This will only pass if something on the DroneBridge side sends data back.
            var result = await smokeTest.WaitForDataAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.True(result.BytesReceived > 0);
        }
    }

    /// <summary>
    /// Receives a MAVLink heartbeat message through the DroneBridge.
    /// </summary>
    [Fact]
    public async Task Should_Receive_MavLink_Heartbeat_Through_DroneBridge()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();

        await connection.StartAsync(TestContext.Current.CancellationToken);
        TaskCompletionSource ts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MavLinkFrame? messageResult = null;
        using var subscription = eventHub.SubscribeAsync<MavLinkFrame>(MavLinkEventTopics.ReceivedFrame, (frame, cts) =>
        {
            if (frame.MessageId == MessageIds.Heartbeat)
            {
                messageResult = frame;
                ts.TrySetResult();
            }

            return Task.CompletedTask;
        });

        await ts.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        messageResult.Should().NotBeNull();
        var frame = messageResult!;
        Assert.Equal(MessageIds.Heartbeat, frame.MessageId);
        Assert.True(frame.SystemId > 0);
    }
}
