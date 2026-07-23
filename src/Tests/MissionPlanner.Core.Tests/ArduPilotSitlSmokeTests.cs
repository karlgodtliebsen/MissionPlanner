using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Provides opt-in real-ArduPilot smoke coverage with strict startup and cleanup bounds.</summary>
public sealed class ArduPilotSitlSmokeTests
{
    /// <summary>Launches, connects, verifies, and stops an available family-specific SITL binary.</summary>
    /// <param name="family">Expected firmware family.</param>
    /// <param name="frame">Supported direct SITL model.</param>
    /// <param name="environmentName">Environment variable containing the executable path.</param>
    [Theory]
    [InlineData(FirmwareFamily.ArduCopter, "quad", "MISSIONPLANNER_SITL_ARDUCOPTER")]
    [InlineData(FirmwareFamily.ArduPlane, "plane", "MISSIONPLANNER_SITL_ARDUPLANE")]
    [InlineData(FirmwareFamily.Rover, "rover", "MISSIONPLANNER_SITL_ROVER")]
    [InlineData(FirmwareFamily.ArduSub, "vectored", "MISSIONPLANNER_SITL_ARDUSUB")]
    public async Task AvailableFamilyLaunchesConnectsAndStops(
        FirmwareFamily family,
        string frame,
        string environmentName)
    {
        var executable = Environment.GetEnvironmentVariable(environmentName);
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            Assert.Skip($"Set {environmentName} to a verified local SITL executable to run this smoke test.");
        }

        var mavLinkPort = AvailableUdpPort();
        var consolePort = AvailableTcpPort();
        var logRoot = Path.Combine(Path.GetTempPath(), "MissionPlannerTests", "RealSitl", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationSettings:Channel"] = "UDP",
            ["ApplicationSettings:BaudRate"] = "115200",
            ["ApplicationSettings:Host"] = "127.0.0.1",
            ["ApplicationSettings:Port"] = mavLinkPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Simulation:HeartbeatTimeoutSeconds"] = "15",
            ["Simulation:StopTimeoutSeconds"] = "8",
            ["Simulation:LogRootDirectory"] = logRoot
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ISetupCompletionStore>());
        services.AddSingleton(Substitute.For<IFirmwarePackageCache>());
        services.AddSingleton(Substitute.For<IDispatcher>());
        services.AddSingleton(Substitute.For<IFileSaver>());
        services.AddApplicationConfiguration(configuration);
        await using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<ISimulationSessionManager>();
        var profile = SimulatorProfile.CreateDefault() with
        {
            Name = $"{family} opt-in smoke",
            FirmwareFamily = family,
            FrameModel = frame,
            Endpoints =
            [
                new SimulationEndpoint("MAVLink", SimulationEndpointTransport.Udp, "127.0.0.1", mavLinkPort),
                new SimulationEndpoint("Console", SimulationEndpointTransport.Tcp, "127.0.0.1", consolePort)
            ],
            Binary = new SimulatorBinaryReference("opt-in", Path.GetFullPath(executable), "external"),
            LaunchSettings = ArduPilotLaunchSettings.Default
        };
        using var totalTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        totalTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        var succeeded = false;

        try
        {
            var started = await manager.StartAsync(profile, totalTimeout.Token);
            started.State.Should().Be(SimulationSessionState.Running, started.Failure);
            succeeded = true;
        }
        finally
        {
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await manager.StopAsync(cleanupTimeout.Token);
            if (succeeded && Directory.Exists(logRoot))
            {
                Directory.Delete(logRoot, recursive: true);
            }
        }
    }

    private static int AvailableUdpPort()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }

    private static int AvailableTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
