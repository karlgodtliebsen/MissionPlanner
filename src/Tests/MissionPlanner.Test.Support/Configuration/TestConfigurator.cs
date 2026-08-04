using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Library.Configuration;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.Simulator.SmokeTests;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Configuration;
using Serilog;

namespace MissionPlanner.Test.Support.Configuration;

/// <summary>
/// Provides methods for configuring test services and dependencies.
/// </summary>
public static class TestConfigurator
{
    private static readonly Lock UdpPortReservationGate = new();
    private static readonly HashSet<int> ReservedUdpPorts = [];

    /// <summary>
    /// Adds test configuration services to the service collection.
    /// </summary>
    public static IServiceCollection AddTestConfiguration(ITestOutputHelper? output)
    {
        List<IConfigurationSource> configurationSources = [new JsonConfigurationSource { Path = "appsettings.test.json", Optional = false, ReloadOnChange = false }];

        ConfigurationBuilder builder = new();
        foreach (var source in configurationSources)
        {
            builder.Sources.Add(source);
        }

        IServiceCollection services = new ServiceCollection();
        IConfiguration configuration = builder.Build();
        services.AddTestConfiguration(configuration, output);
        return services;
    }

    /// <summary>
    /// Replaces the default UDP transport endpoint with loopback ports reserved for one test instance.
    /// </summary>
    /// <param name="services">The test service collection to configure.</param>
    /// <returns>The isolated transport endpoint registered with the service collection.</returns>
    public static TransportEndpoint ConfigureIsolatedUdpTransport(this IServiceCollection services)
    {
        var endpoint = new TransportEndpoint(
            remotePort: ReserveAvailableUdpPort(),
            remoteHost: IPAddress.Loopback.ToString(),
            localPort: ReserveAvailableUdpPort(),
            localHost: IPAddress.Loopback.ToString());

        services.RemoveAll<IOptions<TransportEndpoint>>();
        services.AddSingleton(Options.Create(endpoint));
        return endpoint;
    }

    private static int ReserveAvailableUdpPort()
    {
        lock (UdpPortReservationGate)
        {
            while (true)
            {
                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                var port = ((IPEndPoint)socket.LocalEndPoint!).Port;
                if (ReservedUdpPorts.Add(port))
                {
                    return port;
                }
            }
        }
    }


    /// <summary>
    /// Adds MAVLink Transport services and dependencies to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to which MAVLink services will be added.</param>
    /// <param name="configuration">The configuration to be used for MAVLink services.</param>
    /// <param name="output"></param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTestConfiguration(this IServiceCollection services, IConfiguration configuration, ITestOutputHelper? output)
    {
        services
            .AddLibraryServices()
            .AddEventHubServices()
            .AddDomainServices(configuration)
            .AddMavLinkTransportServices(configuration)
            .AddFirmwareServices(configuration)
            .AddMavLinkServices(configuration)
            ;

        services.TryAddTransient<ITransportSmokeTestService, TransportSmokeTestService>();
        services.AddDefaultTestLogging(configuration, output);
        return services;
    }

    /// <summary>
    /// Provides the public API for AddDefaultTestLogging.
    /// </summary>
    public static IServiceCollection AddDefaultTestLogging(this IServiceCollection services, IConfiguration configuration, ITestOutputHelper? output)
    {
        services.AddLogging((ILoggingBuilder loggingBuilder) =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(LogLevel.Trace);
            loggingBuilder.AddFilter("Microsoft", LogLevel.Warning);
            loggingBuilder.AddFilter("System", LogLevel.Warning);
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
            loggingBuilder.AddSerilog();
            services.AddSerilog(configuration);
            if (output is not null)
            {
                services.AddSingleton<ILoggerProvider>(new XUnitConsoleMsLoggerProvider(output));
            }
        });
        return services;
    }

    /// <summary>
    /// Configures test services and dependencies using the specified <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="services">The service provider to which test services will be added.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseTestConfiguration(this IServiceProvider services)
    {
        var endPoint = services.GetRequiredService<IOptions<TransportEndpoint>>();
        //endPoint.Value.LocalPort =  Rnd.Next(1024, 655
        //endPoint.Value.RemotePort = endPoint.Value.LocalPort + 1; 

        var logger = services.GetRequiredService<ILogger<ServiceProvider>>();

        logger.LogInformation($"Test configuration initialized. UDP local:  {endPoint.Value.LocalHost}:{endPoint.Value.LocalPort}");
        logger.LogInformation($"Test configuration initialized. UDP remote: {endPoint.Value.RemoteHost}:{endPoint.Value.RemotePort}");


        services
            .UseMavLinkServices()
            .UseDomainServices()
            ;
        return services;
    }
}
