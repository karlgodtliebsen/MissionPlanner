namespace MissionPlanner.App.Services;

/// <summary>Reads and atomically replaces one opaque text document in the user's local application-data folder.</summary>
public class JsonDocumentFileStore
{
    private readonly SemaphoreSlim accessGate = new(1, 1);
    private readonly string filePath;

    /// <summary>Creates a store for a file below the default per-user application-data location.</summary>
    /// <param name="fileName">File name inside the "MissionPlanner Next Gen" folder.</param>
    protected JsonDocumentFileStore(string fileName)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MissionPlanner Next Gen",
            fileName), true)
    {
    }

    internal JsonDocumentFileStore(string filePath, bool _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <summary>Reads the persisted document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/> when none has been saved.</returns>
    protected async ValueTask<string?> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var document = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(document) ? null : document;
        }
        finally
        {
            accessGate.Release();
        }
    }

    /// <summary>Atomically replaces the persisted document.</summary>
    /// <param name="document">The serialized document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected async ValueTask WriteDocumentAsync(string document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(filePath)
                ?? throw new InvalidOperationException("The document path has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, document, cancellationToken).ConfigureAwait(false);
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
