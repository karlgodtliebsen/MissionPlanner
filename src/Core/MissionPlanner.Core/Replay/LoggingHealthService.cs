using MissionPlanner.Library.Logging;
using Serilog.Events;

namespace MissionPlanner.Core.Replay;

/// <summary>Lightweight immutable health for both GCS logging subsystems.</summary>
/// <param name="Telemetry">Current recorder lifecycle, name, byte count, and start time.</param>
/// <param name="MinimumLevel">Application recording threshold.</param>
/// <param name="FileEnabled">Whether application file logging is configured.</param>
/// <param name="CurrentFile">Expected active rolling file, when available.</param>
/// <param name="MemoryCount">Retained structured events.</param>
/// <param name="MemoryCapacity">Maximum retained structured events.</param>
public sealed record LoggingHealthSnapshot(TelemetryRecordingStatus Telemetry, LogEventLevel MinimumLevel,
    bool FileEnabled, string? CurrentFile, int MemoryCount, int MemoryCapacity);

/// <summary>Reports health without subscribing to or owning either logging pipeline.</summary>
public sealed class LoggingHealthService(TelemetryRecordingService telemetry, ApplicationLogBuffer buffer,
    IApplicationLogLevelController levels, ApplicationLogFileState files)
{
    /// <summary>Reads a current lightweight snapshot.</summary>
    public LoggingHealthSnapshot Snapshot => new(telemetry.Current, levels.MinimumLevel,
        files.FileEnabled, files.CurrentFile, buffer.Count, buffer.Capacity);
}
