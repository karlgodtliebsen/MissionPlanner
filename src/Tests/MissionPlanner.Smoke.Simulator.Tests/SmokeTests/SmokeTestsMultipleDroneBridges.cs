using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Library;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Simulator.SmokeTests;
using MissionPlanner.Smoke.Simulator.Tests.Configuration;
using MissionPlanner.Transport;

namespace MissionPlanner.Smoke.Simulator.Tests.SmokeTests;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class SmokeTestsMultipleDroneBridges
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;
    private readonly IList<TransportEndpoint> endPoints = [];
    private readonly ILogger<SmokeTestsMultipleDroneBridges> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmokeTestsDroneBridge"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public SmokeTestsMultipleDroneBridges(ITestOutputHelper output)
    {
        this.output = output;

        var services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
        var localPort = 14550;
        var remotePort = 14551;
        var localHost = "0.0.0.0";

        var remoteHost1 = "192.168.1.248";
        var remoteHost2 = "192.168.1.217";

        var endPoint1 = new TransportEndpoint("udp", remotePort, remoteHost1, localPort, localHost, 512);
        var endPoint2 = new TransportEndpoint("udp", remotePort, remoteHost2, localPort, localHost, 512);

        //endPoints = [endPoint1, endPoint2];

        endPoints = [endPoint2];


        //IOptions<TransportEndpoint[]> endPoints = serviceProvider.GetRequiredService<IOptions<TransportEndpoint[]>>();
        //for tcp: 127.0.0.1 on port 5760

        logger = serviceProvider.GetRequiredService<ILogger<SmokeTestsMultipleDroneBridges>>();

        logger.LogInformation("Test configuration initialized. UDP local:  {LocalHost}:{LocalPort}", endPoint1.LocalHost, endPoint1.LocalPort);

        foreach (var endPoint in endPoints)
        {
            logger.LogInformation("Test configuration initialized. UDP remote:  {RemoteHost}:{RemotePort}", endPoint.RemoteHost, endPoint.RemotePort);
        }
    }


    /// <summary>
    /// Sends a TCP  probe to the DroneBridge without expecting any response.
    /// </summary>
    [Fact]
    public async Task Should_Send_Tcp_Probe_To_DroneBridges_Without_Error()
    {
        foreach (var endPoint in endPoints)
        {
            logger.LogInformation("Sending Payload using TCP to RemoteHost:  {RemoteHost}:{RemotePort}", endPoint.RemoteHost, endPoint.RemotePort);

            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(endPoint.RemoteHost, 5760, TestContext.Current.CancellationToken);

            Assert.True(tcpClient.Connected);
        }
    }


    /// <summary>
    /// Sends a UDP probe to the DroneBridge without expecting any response.
    /// </summary>
    [Fact]
    public async Task Should_Factorize_Connection()
    {
        var transport = serviceProvider.GetRequiredService<IMavLinkTransport>();

        await transport.ConnectAsync(TestContext.Current.CancellationToken);

        foreach (var endPoint in endPoints)
        {
            var targetAddress = string.IsNullOrWhiteSpace(endPoint.RemoteHost)
                ? IPAddress.Any
                : IPAddress.Parse(endPoint.RemoteHost);

            var ipEndPoint = new IPEndPoint(targetAddress, endPoint.RemotePort);
            var payload = TransportProbePayloads.CreateAsciiProbe();
            await transport.WriteAsync(payload, ipEndPoint.ToTransportEndPoint("udp"), TestContext.Current.CancellationToken);
        }
    }


    /// <summary>
    /// Tests that a vehicle can be armed through IVehicleService using the full MAVLink simulator pipeline.
    /// </summary>
    [Fact]
    public async Task Should_Arm_Vehicle_Through_VehicleService_When_Command_Is_Acked()
    {
        var vehicleId = new VehicleId(1, 1);
        await using var connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        var messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();

        var cn = connection;
        _ = Task.Run(() => cn.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _ = Task.Run(() => messagePump.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await EventuallyAsync(
            () =>
            {
                var vehicles = vehicleService.GetVehicles();
                if (!vehicles.Any())
                {
                    throw new DomainException("No Vehicles Found");
                }
            },
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);

        var response = await vehicleService.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(VehicleCommandResult.Accepted, response.Result);

        await EventuallyAsync(
            () =>
            {
                var vehicles = vehicleService.GetVehicles();
                if (!vehicles.Any())
                {
                    throw new DomainException("No Vehicles Found");
                }
            },
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);
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

    /// <summary>
    /// Sends a UDP probe to the DroneBridge without expecting any response.
    /// </summary>
    [Fact]
    public async Task Should_Send_Udp_Probe_To_DroneBridges_Without_Error()
    {
        var smokeTest = serviceProvider.GetRequiredService<ITransportSmokeTestService>();
        foreach (var endPoint in endPoints)
        {
            logger.LogInformation("Sending Payload using UDP to RemoteHost:  {RemoteHost}:{RemotePort}", endPoint.RemoteHost, endPoint.RemotePort);

            var targetAddress = string.IsNullOrWhiteSpace(endPoint.RemoteHost)
                ? IPAddress.Any
                : IPAddress.Parse(endPoint.RemoteHost);

            var ipEndPoint = new IPEndPoint(targetAddress, endPoint.RemotePort);

            var payload = TransportProbePayloads.CreateAsciiProbe();
            await smokeTest.SendProbeAsync(payload, ipEndPoint, TestContext.Current.CancellationToken);
        }

        Assert.True(true);
    }
}
