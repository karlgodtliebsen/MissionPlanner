using Microsoft.Extensions.Configuration;

namespace MissionPlanner.Library.Logging;

/// <summary>Exposes configured file logging and conservatively protects current rolling files.</summary>
public sealed class ApplicationLogFileState
{
    private readonly string stem;
    private readonly string extension;
    private readonly string interval;
    private readonly ILogPathProvider? paths;

    /// <summary>Reads the configured File sink without constructing or reflecting into a logger.</summary>
    public ApplicationLogFileState(IConfiguration configuration, ILogPathProvider? paths = null, bool browser = false)
    {
        this.paths = paths;
        var sink = configuration.GetSection("Serilog:WriteTo").GetChildren()
            .FirstOrDefault(section => string.Equals(section["Name"], "File", StringComparison.OrdinalIgnoreCase));
        FileEnabled = !browser && !OperatingSystem.IsBrowser() && paths is not null && sink is not null;
        var path = sink?["Args:path"];
        var name = path is null or "{ApplicationLogPath}" ? "MissionPlanner.NextGen.Application-.log" : Path.GetFileName(path);
        stem = Path.GetFileNameWithoutExtension(name);
        extension = Path.GetExtension(name);
        interval = sink?["Args:rollingInterval"] ?? "Infinite";
    }

    /// <summary>Whether this host is configured to persist application files.</summary>
    public bool FileEnabled { get; }

    /// <summary>Expected current rolling file (size-roll suffixes may also be active).</summary>
    public string? CurrentFile => FileEnabled ? Path.Combine(paths!.ApplicationLogDirectory, CurrentPrefix + extension) : null;

    /// <summary>Protects all size rolls in the current time interval as potentially active.</summary>
    public bool IsActive(LogStorageItem item) => FileEnabled && item.Area == LogStorageArea.Application &&
        item.Name.StartsWith(CurrentPrefix, StringComparison.OrdinalIgnoreCase);

    private string CurrentPrefix => stem + (interval.ToLowerInvariant() switch
    {
        "year" => System.DateTime.Now.ToString("yyyy"),
        "month" => System.DateTime.Now.ToString("yyyyMM"),
        "day" => System.DateTime.Now.ToString("yyyyMMdd"),
        "hour" => System.DateTime.Now.ToString("yyyyMMddHH"),
        "minute" => System.DateTime.Now.ToString("yyyyMMddHHmm"),
        _ => ""
    });
}
