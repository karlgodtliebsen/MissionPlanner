using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Library.Configuration;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.Simulator.SmokeTests;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Configuration;

namespace MissionPlanner.Smoke.Simulator.Tests.Configuration;

/// <summary>
/// 
/// </summary>
public static class TestConfigurator
{
    /// <summary>
    /// Adds test configuration services to the service collection.
    /// </summary>
    public static IServiceCollection AddTestConfiguration()
    {
        List<IConfigurationSource> configurationSources = [new JsonConfigurationSource { Path = "appsettings.test.json", Optional = false, ReloadOnChange = false }];

        ConfigurationBuilder builder = new();
        //var configurationBuilder = builder.Configuration;

        foreach (var source in configurationSources)
        {
            builder.Sources.Add(source);
        }

        IServiceCollection services = new ServiceCollection();
        IConfiguration configuration = builder.Build();
        services.AddTestConfiguration(configuration);
        return services;
    }


    /// <summary>
    /// Adds MAVLink Transport services and dependencies to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to which MAVLink services will be added.</param>
    /// <param name="configuration">The configuration to be used for MAVLink services.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTestConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddLibraryServices()
            .AddEventHubServices()
            .AddDomainServices(configuration)
            .AddMavLinkTransportServices(configuration)
            .AddMavLinkServices(configuration);

        services.TryAddTransient<ITransportSmokeTestService, TransportSmokeTestService>();

        return services;
    }

    /// <summary>
    /// Provides the public API for AddDefaultTestLogging.
    /// </summary>
    public static IServiceCollection AddDefaultTestLogging(this IServiceCollection services, IConfiguration configuration, ITestOutputHelper? output)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(LogLevel.Trace);

            loggingBuilder.AddFilter("Microsoft", LogLevel.Warning);
            loggingBuilder.AddFilter("System", LogLevel.Warning);

            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
            if (output is not null)
            {
                services.AddSingleton<ILoggerProvider>(new XUnitConsoleMsLoggerProvider(output));
            }
        });
        return services;
    }

    /// <summary>
    /// Provides the public API for AddDefaultTestLogging.
    /// </summary>
    public static IServiceCollection AddDefaultTestLogging(this IServiceCollection services, ITestOutputHelper? output)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(LogLevel.Trace);

            loggingBuilder.AddFilter("Microsoft", LogLevel.Warning);
            loggingBuilder.AddFilter("System", LogLevel.Warning);

            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
            if (output is not null)
            {
                services.AddSingleton<ILoggerProvider>(new XUnitConsoleMsLoggerProvider(output));
            }
        });
        return services;
    }

    private static readonly Random Rnd = new(1024);

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
