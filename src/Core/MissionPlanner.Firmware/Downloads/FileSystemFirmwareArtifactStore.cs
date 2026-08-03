namespace MissionPlanner.Firmware.Downloads;

/// <summary>Provides atomic local artifact storage behind the storage abstraction.</summary>
public sealed class FileSystemFirmwareArtifactStore : IFirmwareArtifactStore
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "MissionPlanner", "FirmwareArtifacts");

    /// <inheritdoc />
    public Task<IFirmwareStoredArtifact?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = DataPath(cacheKey);
        var metadata = MetadataPath(cacheKey);
        if (!File.Exists(data) || !File.Exists(metadata)) return Task.FromResult<IFirmwareStoredArtifact?>(null);
        try
        {
            var fields = File.ReadAllLines(metadata);
            if (fields.Length != 5) return Task.FromResult<IFirmwareStoredArtifact?>(null);
            var model = new FirmwareArtifactMetadata(fields[0], new Uri(fields[1]), DateTimeOffset.Parse(fields[2], null, System.Globalization.DateTimeStyles.RoundtripKind), long.Parse(fields[3]), fields[4]);
            return Task.FromResult<IFirmwareStoredArtifact?>(new Stored(data, model));
        }
        catch (Exception exception) when (exception is IOException or FormatException or UriFormatException) { return Task.FromResult<IFirmwareStoredArtifact?>(null); }
    }

    /// <inheritdoc />
    public Task<IFirmwareArtifactWriter> CreateTemporaryAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{cacheKey}.{Guid.NewGuid():N}.partial");
        IFirmwareArtifactWriter writer = new Writer(this, cacheKey, path, new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous));
        return Task.FromResult(writer);
    }

    private string DataPath(string key) => Path.Combine(root, $"{key}.bin");
    private string MetadataPath(string key) => Path.Combine(root, $"{key}.meta");

    private sealed class Writer(FileSystemFirmwareArtifactStore owner, string key, string path, FileStream stream) : IFirmwareArtifactWriter
    {
        private bool committed;
        public Stream Stream => stream;
        public async Task<IFirmwareStoredArtifact> CommitAsync(FirmwareArtifactMetadata metadata, CancellationToken cancellationToken = default)
        {
            if (committed) throw new InvalidOperationException("Artifact writer is already committed.");
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Dispose();
            var destination = owner.DataPath(key);
            File.Move(path, destination, true);
            await File.WriteAllLinesAsync(owner.MetadataPath(key), [metadata.CacheKey, metadata.SourceUri.AbsoluteUri, metadata.DownloadedAt.ToString("O"), metadata.Size.ToString(System.Globalization.CultureInfo.InvariantCulture), metadata.Sha256], cancellationToken).ConfigureAwait(false);
            committed = true;
            return new Stored(destination, metadata);
        }
        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            if (!committed) File.Delete(path);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Stored(string path, FirmwareArtifactMetadata metadata) : IFirmwareStoredArtifact
    {
        public FirmwareArtifactMetadata Metadata => metadata;
        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream result = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult(result);
        }
    }
}
