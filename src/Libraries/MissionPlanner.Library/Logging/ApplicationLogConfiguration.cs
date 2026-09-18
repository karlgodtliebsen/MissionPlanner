using Microsoft.Extensions.Configuration;

namespace MissionPlanner.Library.Logging;

/// <summary>Resolves platform-specific sink configuration before a Serilog logger is constructed.</summary>
public static class ApplicationLogConfiguration
{
    /// <summary>Replaces only the application-path placeholder; browser configurations exclude File sinks and assembly hints.</summary>
    public static IConfiguration Resolve(IConfiguration configuration, ILogPathProvider? paths, bool browser)
    {
        var values = configuration.AsEnumerable().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var sink in configuration.GetSection("Serilog:WriteTo").GetChildren())
        {
            if (!string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (browser)
            {
                foreach (var key in values.Keys.Where(key => key == sink.Path || key.StartsWith(sink.Path + ":", StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    values.Remove(key);
                }
            }
            else if (sink["Args:path"] == "{ApplicationLogPath}")
            {
                var directory = paths?.ApplicationLogDirectory ?? throw new InvalidOperationException("Desktop log paths were not registered.");
                Directory.CreateDirectory(directory);
                values[sink.Path + ":Args:path"] = Path.Combine(directory, "MissionPlanner.NextGen.Application-.log");
            }
        }

        if (browser)
        {
            foreach (var key in values.Keys.Where(key =>
                         key.StartsWith("Serilog:Using:", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(values[key], "Serilog.Sinks.File", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                values.Remove(key);
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
