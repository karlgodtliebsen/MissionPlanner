using MissionPlanner.AvaloniaUI.App.Presentation;

namespace MissionPlanner.AvaloniaUI.App.Services;

/// <summary>Stores the last file-picker directory in the user's local application data.</summary>
public sealed class FilePickerPathStore : IFilePickerPathStore
{
    private readonly SemaphoreSlim accessGate = new(1, 1);
    private readonly string filePath;

    /// <summary>Creates a store using the application's per-user data directory.</summary>
    public FilePickerPathStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MissionPlanner Next Gen",
            "last-file-picker-path.txt"))
    {
    }

    internal FilePickerPathStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var directoryPath = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(directoryPath) ? null : directoryPath.Trim();
        }
        finally
        {
            accessGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var normalizedPath = Path.GetFullPath(directoryPath);

        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(filePath)
                ?? throw new InvalidOperationException("The file-picker settings path has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, normalizedPath, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            accessGate.Release();
        }
    }
}
