using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MissionPlanner.Library.Configuration;

/// <summary>
/// Provides extension methods for configuring logging services in the application.
/// </summary>
public static partial class LoggingLibraryConfigurator
{
    /// <summary>
    /// Adds logging configuration to the service collection with optional customization.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="optionsAction">An optional action to configure logging options.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddLogging(this IServiceCollection services, IConfiguration configuration, Action<IServiceCollection, ILoggingBuilder, IConfiguration>? optionsAction = null)
    {
        services.AddLogging((ILoggingBuilder loggingBuilder) =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(LogLevel.Trace);
            loggingBuilder.AddFilter("Microsoft", LogLevel.Warning);
            loggingBuilder.AddFilter("System", LogLevel.Warning);
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggingBuilder.AddSerilog();
            services.AddSerilog(configuration);
            optionsAction?.Invoke(services, loggingBuilder, configuration);
        });
        return services;
    }

    public static IServiceCollection AddSerilog(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        return services;
    }
}
