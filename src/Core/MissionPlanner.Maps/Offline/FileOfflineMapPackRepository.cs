namespace MissionPlanner.Maps.Offline;

/// <summary>Stores validated packs beneath Maps/Packs/&lt;id&gt;/&lt;version&gt;.</summary>
public sealed class FileOfflineMapPackRepository : IOfflineMapPackRepository
{
    private readonly List<string> diagnostics = [];

    /// <summary>Manifest file name within an installed version.</summary>
    public const string ManifestFileName = "manifest.json";

    private readonly string root;

    /// <summary>Initializes a filesystem pack repository.</summary>
    public FileOfflineMapPackRepository(string applicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataRoot);
        root = Path.Combine(Path.GetFullPath(applicationDataRoot), "Maps", "Packs");
    }

    /// <summary>Gets the repository root.</summary>
    public string RootPath => root;

    /// <summary>Gets diagnostics for corrupt manifests skipped by the most recent enumeration.</summary>
    public IReadOnlyList<string> LastDiagnostics => diagnostics;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<InstalledOfflineMapPack>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var packs = new List<InstalledOfflineMapPack>();
        diagnostics.Clear();
        foreach (var manifestPath in Directory.EnumerateFiles(root, ManifestFileName, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (manifestPath.Contains(Path.DirectorySeparatorChar + ".staging-", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var manifest = OfflineMapPackJson.Deserialize(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
                var directory = Path.GetDirectoryName(manifestPath)!;
                var archive = Path.Combine(directory, manifest.ArchiveFileName);
                if (File.Exists(archive))
                {
                    packs.Add(new InstalledOfflineMapPack(manifest, directory, archive));
                }
                else
                {
                    diagnostics.Add($"Pack manifest '{manifestPath}' references a missing archive.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException or NotSupportedException)
            {
                diagnostics.Add($"Pack manifest '{manifestPath}' was skipped: {exception.Message}");
            }
        }

        return packs.OrderBy(item => item.Manifest.DisplayName, StringComparer.Ordinal).ThenBy(item => item.Manifest.Version, StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<InstalledOfflineMapPack?> FindAsync(string id, string version, CancellationToken cancellationToken = default)
    {
        return (await ListAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(item => item.Manifest.Id == id && item.Manifest.Version == version);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string id, string version, string? activePackId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (StringComparer.Ordinal.Equals(id, activePackId))
        {
            throw new InvalidOperationException("The active offline map pack cannot be removed. Select another basemap first.");
        }

        var path = GetVersionPath(id, version);
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, true);
        }

        var parent = Path.GetDirectoryName(path)!;
        if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Gets a validated version installation path.</summary>
    internal string GetVersionPath(string id, string version)
    {
        var path = Path.GetFullPath(Path.Combine(root, id, version));
        return !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidDataException("Pack path escapes the repository root.")
            : path;
    }
}
