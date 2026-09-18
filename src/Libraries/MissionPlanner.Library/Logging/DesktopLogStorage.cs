namespace MissionPlanner.Library.Logging;

/// <summary>Stores logs under the configured desktop root, using exclusive creation and shared readers.</summary>
public sealed class DesktopLogStorage(ILogPathProvider paths) : ILogStorage
{
    /// <inheritdoc />
    public Task<IReadOnlyList<LogStorageItem>> ListAsync(LogStorageArea area, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = GetDirectory(area);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<LogStorageItem>>([]);
        }

        IReadOnlyList<LogStorageItem> items = new DirectoryInfo(directory).EnumerateFiles()
            .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint) && !file.Name.StartsWith(".write-probe-"))
            .Select(file => new LogStorageItem(file.Name, file.Name, area, file.Length,
                file.CreationTimeUtc, file.LastWriteTimeUtc, file.FullName))
            .OrderByDescending(item => item.Created).ToArray();
        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task<Stream> CreateAsync(LogStorageArea area, string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(area, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Stream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            65536, FileOptions.Asynchronous);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(GetPath(area, id), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task DeleteAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(area, id);
        // Opening exclusively prevents deleting a recording still owned by a writer.
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        File.Delete(path);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<LogExportResult> ExportAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default)
        => new(LogNames.Validate(id), await OpenReadAsync(area, id, cancellationToken).ConfigureAwait(false));

    private string GetDirectory(LogStorageArea area)
    {
        LogNames.ValidateArea(area);
        var directory = area == LogStorageArea.Telemetry ? paths.TelemetryDirectory : paths.ApplicationLogDirectory;
        if (Directory.Exists(directory) && File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("Log directories cannot be symbolic links or junctions.");
        }

        return directory;
    }

    private string GetPath(LogStorageArea area, string id)
    {
        LogNames.Validate(id);
        var path = Path.Combine(GetDirectory(area), id);
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("Log files cannot be symbolic links.");
        }

        return path;
    }
}
