using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace MissionPlanner.Library.Configuration;

/// <summary>Configures Serilog from appsettings and adds platform diagnostics services.</summary>
public static partial class LoggingLibraryConfigurator
{
    /// <summary>Adds one Serilog provider and optional host customization.</summary>
    public static IServiceCollection AddLogging(this IServiceCollection services, IConfiguration configuration,
        Action<IServiceCollection, ILoggingBuilder, IConfiguration>? optionsAction = null)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            // Serilog, including its runtime switch, owns severity filtering.
            builder.SetMinimumLevel(LogLevel.Trace);
            optionsAction?.Invoke(services, builder, configuration);
        });
        return services.AddSerilog(configuration);
    }

    /// <summary>Registers the structured buffer, runtime switch, and configuration-driven logger.</summary>
    public static IServiceCollection AddSerilog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogStorage();
        services.TryAddSingleton(provider => new ApplicationLogFileState(configuration, provider.GetService<ILogPathProvider>()));
        services.TryAddTransient<ApplicationLogHistory>();
        services.TryAddSingleton(_ => new ApplicationLogBuffer(
            configuration.GetValue<int?>("ApplicationLogging:MemoryCapacity") ?? 5000));
        services.TryAddSingleton(_ => new LoggingLevelSwitch(
            Enum.TryParse<LogEventLevel>(configuration["Serilog:MinimumLevel:Default"] ??
                configuration["Serilog:MinimumLevel"], true, out var level) ? level : LogEventLevel.Information));
        services.TryAddSingleton<IApplicationLogLevelController, ApplicationLogLevelController>();
        services.TryAddSingleton<Serilog.ILogger>(provider =>
        {
            var resolved = ApplicationLogConfiguration.Resolve(configuration,
                provider.GetService<ILogPathProvider>(), OperatingSystem.IsBrowser());
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(resolved)
                .MinimumLevel.ControlledBy(provider.GetRequiredService<LoggingLevelSwitch>())
                .Enrich.FromLogContext()
                .WriteTo.Sink(provider.GetRequiredService<ApplicationLogBuffer>())
                .CreateLogger();
            Log.Logger = logger;
            return logger;
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ApplicationSerilogProvider>());
        return services;
    }

    private sealed class ApplicationSerilogProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly SerilogLoggerProvider provider;

        public ApplicationSerilogProvider(Serilog.ILogger logger)
        {
            provider = new SerilogLoggerProvider(logger, dispose: false);
        }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => provider.CreateLogger(categoryName);
        public void Dispose() => provider.Dispose();
        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            if (provider is ISupportExternalScope external)
            {
                external.SetScopeProvider(scopeProvider);
            }
        }
    }
}
