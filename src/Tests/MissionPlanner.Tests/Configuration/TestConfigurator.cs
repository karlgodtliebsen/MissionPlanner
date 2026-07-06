using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.Configuration;

namespace MissionPlanner.Tests.Configuration;

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
        ConfigurationBuilder builder = new();
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
            ;

        return services;
    }

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
}
