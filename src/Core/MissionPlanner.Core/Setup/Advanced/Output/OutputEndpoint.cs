namespace MissionPlanner.Core.Setup.Advanced.Output;

/// <summary>Explicitly supported output-only transports; no listening sockets are created.</summary>
public enum OutputEndpointKind
{
    /// <summary>An exclusive serial output.</summary>
    Serial,
    /// <summary>A UDP destination.</summary>
    Udp,
    /// <summary>An outbound TCP connection.</summary>
    Tcp
}

/// <summary>A validated output profile with no native transport types.</summary>
public sealed record OutputEndpoint(OutputEndpointKind Kind, string Address, int Port = 14551, int BaudRate = 57600)
{
    /// <summary>Gets the ownership identity; serial baud changes cannot open a second owner.</summary>
    public string Identity => $"{Kind}:{Address.Trim().ToLowerInvariant()}" + (Kind == OutputEndpointKind.Serial ? "" : $":{Port}");
    /// <summary>Gets a display summary without credentials.</summary>
    public string Summary => Kind == OutputEndpointKind.Serial ? $"Serial {Address} at {BaudRate} baud" : $"{Kind} {Address}:{Port}";
    /// <summary>Returns an actionable validation error, or null.</summary>
    public string? Validate()
    {
        if (!Enum.IsDefined(Kind)) { return "Select a supported output transport."; }
        if (string.IsNullOrWhiteSpace(Address) || Address.Length > 253 || Address.Any(char.IsControl) || Address != Address.Trim())
        {
            return "Enter a valid port name or host, without surrounding whitespace.";
        }
        if (Kind == OutputEndpointKind.Serial)
        {
            return BaudRate is < 1200 or > 4_000_000 ? "Baud rate must be between 1200 and 4000000." : null;
        }
        if (Port is < 1 or > 65535) { return "Destination port must be between 1 and 65535."; }
        return Uri.CheckHostName(Address) == UriHostNameType.Unknown ? "Enter a hostname or IP address, without a URL scheme or credentials." : null;
    }
}

/// <summary>Platform-owned sink. Abort must release any pending write so disposal can be awaited.</summary>
public interface IOutputSink : IAsyncDisposable
{
    /// <summary>Writes one complete frame or text batch, honouring cancellation.</summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token);
    /// <summary>Synchronously closes the native handle and interrupts pending I/O.</summary>
    void Abort();
}

/// <summary>Opens output-only sinks behind the native/browser platform boundary.</summary>
public interface IOutputSinkFactory
{
    /// <summary>Returns an availability or validation reason without opening a device.</summary>
    string? UnavailableReason(OutputEndpoint endpoint);
    /// <summary>Opens one cancellable sink; failures must dispose partially opened native handles.</summary>
    Task<IOutputSink> OpenAsync(OutputEndpoint endpoint, CancellationToken token);
}

/// <summary>Owns one output endpoint across Mirror, NMEA and other output tools.</summary>
public sealed class OutputEndpointOwners
{
    private readonly HashSet<string> owned = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Acquires exclusive use, rejecting a second owner before opening hardware.</summary>
    public IDisposable Acquire(string identity)
    {
        lock (owned)
        {
            if (!owned.Add(identity)) { throw new InvalidOperationException("This output endpoint is already in use by another tool."); }
            return new Lease(this, identity);
        }
    }
    private sealed class Lease(OutputEndpointOwners owners, string identity) : IDisposable
    {
        public void Dispose() { lock (owners.owned) { owners.owned.Remove(identity); } }
    }
}

/// <summary>Bounds resource use and reconnect behaviour for all output sessions.</summary>
public sealed record OutputSessionOptions(int Capacity = 256, int ReconnectAttempts = 0, int ReconnectDelaySeconds = 2, int WriteTimeoutSeconds = 3)
{
    /// <summary>Validates resource limits before starting.</summary>
    public void Validate()
    {
        if (Capacity is < 1 or > 4096 || ReconnectAttempts is < 0 or > 5 || ReconnectDelaySeconds is < 1 or > 30 || WriteTimeoutSeconds is < 1 or > 10)
        {
            throw new ArgumentException("Invalid queue, reconnect or write-timeout settings.");
        }
    }
}

/// <summary>Non-sensitive output session state and accounting.</summary>
public sealed record OutputSessionSnapshot(string State, string Endpoint, long Frames, long Bytes,
    long DroppedFrames, long DroppedBytes, DateTimeOffset? LastWrite, string? Error);
