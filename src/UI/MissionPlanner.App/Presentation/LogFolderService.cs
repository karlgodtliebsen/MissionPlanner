using System.Diagnostics;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.App.Presentation;

/// <summary>Desktop folder launching with an explicit browser capability boundary.</summary>
public sealed class LogFolderService(ILogPathProvider? paths = null) : ILogFolderService
{
    /// <inheritdoc />
    public bool IsSupported => !OperatingSystem.IsBrowser() && paths is not null;

    /// <inheritdoc />
    public Task OpenAsync(LogStorageArea area)
    {
        if (!IsSupported)
        {
            throw new NotSupportedException("This platform does not expose log directories.");
        }

        var directory = area == LogStorageArea.Telemetry ? paths!.TelemetryDirectory : paths!.ApplicationLogDirectory;
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
