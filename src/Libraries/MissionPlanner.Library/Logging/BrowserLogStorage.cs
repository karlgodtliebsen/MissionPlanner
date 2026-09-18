namespace MissionPlanner.Library.Logging;

/// <summary>Bounded session-only storage requiring no browser or desktop filesystem APIs.</summary>
public sealed class BrowserLogStorage : ILogStorage
{
    private readonly object gate = new();
    private readonly Dictionary<(LogStorageArea Area, string Name), Entry> entries = [];
    private readonly long capacity;
    private readonly int maximumFiles;
    private long bytes;

    /// <summary>Creates a session store. Quota exhaustion fails writes without evicting recordings.</summary>
    public BrowserLogStorage(long capacity = 32 * 1024 * 1024, int maximumFiles = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFiles);
        this.capacity = capacity;
        this.maximumFiles = maximumFiles;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogStorageItem>> ListAsync(LogStorageArea area, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogNames.ValidateArea(area);
        lock (gate)
        {
            IReadOnlyList<LogStorageItem> result = entries.Where(pair => pair.Key.Area == area)
                .Select(pair => new LogStorageItem(pair.Key.Name, pair.Key.Name, area,
                    pair.Value.Data.Length, pair.Value.Created, pair.Value.Modified))
                .OrderByDescending(item => item.Created).ToArray();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<Stream> CreateAsync(LogStorageArea area, string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogNames.ValidateArea(area);
        LogNames.Validate(fileName);
        lock (gate)
        {
            if (entries.ContainsKey((area, fileName)))
            {
                throw new IOException("A log with this name already exists.");
            }

            if (entries.Count >= maximumFiles)
            {
                throw new IOException("Browser log file quota reached. Export and delete older logs.");
            }

            var entry = new Entry();
            entries.Add((area, fileName), entry);
            return Task.FromResult<Stream>(new Writer(this, entry));
        }
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var entry = Find(area, id);
            return Task.FromResult<Stream>(new MemoryStream(entry.Data.ToArray(), writable: false));
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var entry = Find(area, id);
            if (entry.Active)
            {
                throw new IOException("Cannot delete a log while it is being written.");
            }

            bytes -= entry.Data.Length;
            entries.Remove((area, id));
            entry.Data.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<LogExportResult> ExportAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default)
        => new(LogNames.Validate(id), await OpenReadAsync(area, id, cancellationToken).ConfigureAwait(false));

    private Entry Find(LogStorageArea area, string id)
    {
        LogNames.ValidateArea(area);
        LogNames.Validate(id);
        return entries.TryGetValue((area, id), out var entry) ? entry : throw new FileNotFoundException("Log not found.", id);
    }

    private sealed class Entry
    {
        internal readonly MemoryStream Data = new();
        internal readonly DateTimeOffset Created = DateTimeOffset.UtcNow;
        internal DateTimeOffset Modified = DateTimeOffset.UtcNow;
        internal bool Active = true;
    }

    private sealed class Writer(BrowserLogStorage owner, Entry entry) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => entry.Active;
        public override long Length => entry.Data.Length;
        public override long Position { get => Length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            lock (owner.gate)
            {
                ObjectDisposedException.ThrowIf(!entry.Active, this);
                if (buffer.Length > owner.capacity - owner.bytes)
                {
                    throw new IOException("Browser log byte quota reached. Export and delete older logs.");
                }

                entry.Data.Write(buffer);
                owner.bytes += buffer.Length;
                entry.Modified = DateTimeOffset.UtcNow;
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            lock (owner.gate)
            {
                entry.Active = false;
            }

            base.Dispose(disposing);
        }
    }
}
