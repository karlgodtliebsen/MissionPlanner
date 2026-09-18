namespace MissionPlanner.Library.Logging;

/// <summary>Resolves and probes a writable desktop root only when logs are first used.</summary>
public sealed class DesktopLogPathProvider : ILogPathProvider
{
    private readonly Lazy<string> root;

    /// <summary>Uses Documents, local application data, then the user profile.</summary>
    public DesktopLogPathProvider() : this(Environment.GetFolderPath)
    {
    }

    /// <summary>Allows platform folder lookup to be supplied by a host or test.</summary>
    public DesktopLogPathProvider(Func<Environment.SpecialFolder, string> getFolder)
    {
        ArgumentNullException.ThrowIfNull(getFolder);
        root = new Lazy<string>(() => Resolve(getFolder));
    }

    /// <inheritdoc />
    public string LogsRoot => root.Value;

    /// <inheritdoc />
    public string TelemetryDirectory => LogsRoot;

    /// <inheritdoc />
    public string ApplicationLogDirectory => Path.Combine(LogsRoot, "application");

    private static string Resolve(Func<Environment.SpecialFolder, string> getFolder)
    {
        var failures = new List<Exception>();
        foreach (var folder in new[] { Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.UserProfile })
        {
            var parent = getFolder(folder);
            if (string.IsNullOrWhiteSpace(parent))
            {
                continue;
            }

            try
            {
                var candidate = Path.GetFullPath(Path.Combine(parent, "Mission Planner", "logs"));
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, $".write-probe-{Guid.NewGuid():N}");
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           1, FileOptions.DeleteOnClose))
                {
                }

                return candidate;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                failures.Add(ex);
            }
        }

        throw new IOException("No writable logging location was found in Documents, local application data, or the user profile.",
            new AggregateException(failures));
    }
}
