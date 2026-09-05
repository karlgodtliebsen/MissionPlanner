using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.AvaloniaUI.App.Services;

/// <summary>Persists Planner settings as JSON in the user's local application-data folder.</summary>
public sealed class JsonPlannerSettingsStore : IPlannerSettingsStore
{
    private readonly SemaphoreSlim accessGate = new(1, 1);
    private readonly string filePath;

    /// <summary>Creates a store using the default per-user application-data location.</summary>
    public JsonPlannerSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MissionPlanner Next Gen",
            "planner-settings.json"))
    {
    }

    internal JsonPlannerSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <inheritdoc />
    public async ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
    public async ValueTask WriteAsync(string document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(filePath)
                ?? throw new InvalidOperationException("The Planner settings path has no parent directory.");
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

    /// <inheritdoc />
    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        finally
        {
            accessGate.Release();
        }
    }
}
