namespace MissionPlanner.Library.Logging;

/// <summary>Separates raw telemetry recordings from application diagnostics.</summary>
public enum LogStorageArea
{
    /// <summary>Classic MAVLink tlog recordings and optional metadata.</summary>
    Telemetry,
    /// <summary>Serilog diagnostic files.</summary>
    Application
}

/// <summary>Describes a stored log without requiring a desktop file system.</summary>
/// <param name="Id">Opaque identifier within the logical area.</param>
/// <param name="Name">Display and export file name.</param>
/// <param name="Area">Logical log area.</param>
/// <param name="Size">Current size in bytes.</param>
/// <param name="Created">Creation time in UTC.</param>
/// <param name="Modified">Last modification time in UTC.</param>
/// <param name="PhysicalPath">Desktop path, or null for browser storage.</param>
public sealed record LogStorageItem(string Id, string Name, LogStorageArea Area, long Size,
    DateTimeOffset Created, DateTimeOffset Modified, string? PhysicalPath = null);

/// <summary>An export whose content stream is owned by the caller.</summary>
/// <param name="FileName">Suggested download name.</param>
/// <param name="Content">Readable content; dispose after saving.</param>
public sealed record LogExportResult(string FileName, Stream Content) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>Platform-neutral storage for telemetry and application logs.</summary>
public interface ILogStorage
{
    /// <summary>Lists logs newest first; missing areas are empty.</summary>
    Task<IReadOnlyList<LogStorageItem>> ListAsync(LogStorageArea area, CancellationToken cancellationToken = default);
    /// <summary>Creates a new log exclusively. The caller owns and must dispose the writer.</summary>
    Task<Stream> CreateAsync(LogStorageArea area, string fileName, CancellationToken cancellationToken = default);
    /// <summary>Opens a readable snapshot or shared file. The caller owns the returned stream.</summary>
    Task<Stream> OpenReadAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default);
    /// <summary>Deletes a closed log. Active writers must be closed first.</summary>
    Task DeleteAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default);
    /// <summary>Opens content for export through a platform file-save service.</summary>
    Task<LogExportResult> ExportAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default);
}

/// <summary>Desktop directories, resolved lazily on first use.</summary>
public interface ILogPathProvider
{
    /// <summary>Root directory for all logs.</summary>
    string LogsRoot { get; }
    /// <summary>Directory containing telemetry files.</summary>
    string TelemetryDirectory { get; }
    /// <summary>Directory containing rolling application files.</summary>
    string ApplicationLogDirectory { get; }
}

internal static class LogNames
{
    internal static string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 ||
            name.Any(char.IsControl) || name.EndsWith('.') || name.EndsWith(' '))
        {
            throw new ArgumentException("A log identifier must be a single safe file name.", nameof(name));
        }

        return name;
    }

    internal static void ValidateArea(LogStorageArea area)
    {
        if (!Enum.IsDefined(area))
        {
            throw new ArgumentOutOfRangeException(nameof(area));
        }
    }
}
