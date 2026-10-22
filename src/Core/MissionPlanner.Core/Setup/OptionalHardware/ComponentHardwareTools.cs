using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>How a DroneCAN adapter reaches the CAN bus.</summary>
public enum DroneCanTransportKind
{
    /// <summary>CAN frames tunnelled through the flight controller over MAVLink.</summary>
    MavLinkTunnel,
    /// <summary>A directly attached SLCAN serial adapter.</summary>
    DirectSlcan,
}
/// <summary>Health a DroneCAN node reports in its NodeStatus message.</summary>
public enum DroneCanNodeHealth
{
    /// <summary>The node has not reported a health value.</summary>
    Unknown,
    /// <summary>The node is operating normally.</summary>
    Ok,
    /// <summary>The node reports a minor problem.</summary>
    Warning,
    /// <summary>The node reports a significant problem.</summary>
    Error,
    /// <summary>The node reports a critical failure.</summary>
    Critical,
}
/// <summary>Operating mode a DroneCAN node reports in its NodeStatus message.</summary>
public enum DroneCanNodeMode
{
    /// <summary>The node has not reported a mode.</summary>
    Unknown,
    /// <summary>The node is running normally.</summary>
    Operational,
    /// <summary>The node is starting up.</summary>
    Initialization,
    /// <summary>The node is in maintenance mode, for example while updating firmware.</summary>
    Maintenance,
    /// <summary>The node is offline.</summary>
    Offline,
}
/// <summary>A node discovered on the DroneCAN bus.</summary>
/// <param name="NodeId">The CAN node identifier.</param>
/// <param name="Name">The node name.</param>
/// <param name="Health">The reported node health.</param>
/// <param name="Mode">The reported node mode.</param>
/// <param name="Version">The node software version text.</param>
/// <param name="RxFrames">The number of frames received from the node.</param>
/// <param name="TxFrames">The number of frames sent to the node.</param>
public sealed record DroneCanNode(byte NodeId, string Name, DroneCanNodeHealth Health, DroneCanNodeMode Mode, string Version, long RxFrames, long TxFrames);
/// <summary>A parameter read from a DroneCAN node.</summary>
/// <param name="NodeId">The node the parameter belongs to.</param>
/// <param name="Name">The parameter name.</param>
/// <param name="Value">The current parameter value.</param>
/// <param name="IsWritable">Whether the parameter can be changed.</param>
public sealed record DroneCanParameter(byte NodeId, string Name, object Value, bool IsWritable);

/// <summary>A transport that talks to DroneCAN nodes.</summary>
public interface IDroneCanTransport : IAsyncDisposable
{
    /// <summary>Gets how the transport reaches the CAN bus.</summary>
    DroneCanTransportKind Kind { get; }
    /// <summary>Gets whether the transport is connected.</summary>
    bool IsConnected { get; }
    /// <summary>Connects the transport.</summary>
    Task ConnectAsync(CancellationToken cancellationToken);
    /// <summary>Disconnects the transport.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken);
    /// <summary>Discovers the nodes on the bus.</summary>
    Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken cancellationToken);
    /// <summary>Reads every parameter of a node.</summary>
    Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken cancellationToken);
    /// <summary>Writes one parameter of a node.</summary>
    Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken cancellationToken);
    /// <summary>Restarts a node.</summary>
    Task RestartNodeAsync(byte nodeId, CancellationToken cancellationToken);
}

/// <summary>Creates DroneCAN transports.</summary>
public interface IDroneCanTransportFactory
{
    /// <summary>Creates a transport of the requested kind.</summary>
    IDroneCanTransport Create(DroneCanTransportKind kind);
}

/// <summary>Manages the DroneCAN connection and node operations.</summary>
public interface IDroneCanService : IAsyncDisposable
{
    /// <summary>Gets whether a transport is connected.</summary>
    bool IsConnected { get; }
    /// <summary>Gets the connected transport kind, or null when disconnected.</summary>
    DroneCanTransportKind? TransportKind { get; }
    /// <summary>Connects through the requested transport.</summary>
    Task ConnectAsync(DroneCanTransportKind kind, CancellationToken cancellationToken);
    /// <summary>Discovers the nodes on the bus.</summary>
    Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken cancellationToken);
    /// <summary>Reads every parameter of a node.</summary>
    Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken cancellationToken);
    /// <summary>Writes one parameter of a node.</summary>
    Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken cancellationToken);
    /// <summary>Restarts a node.</summary>
    Task RestartNodeAsync(byte nodeId, CancellationToken cancellationToken);
    /// <summary>Disconnects the current transport.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken);
}

/// <summary>Default DroneCAN service that delegates to the transport created by the factory.</summary>
/// <param name="factory">Creates the transport for the selected kind.</param>
public sealed class DroneCanService(IDroneCanTransportFactory factory) : IDroneCanService
{
    private IDroneCanTransport? transport;
    /// <summary>Gets whether a transport is connected.</summary>
    public bool IsConnected => transport?.IsConnected == true;
    /// <summary>Gets the connected transport kind, or null when disconnected.</summary>
    public DroneCanTransportKind? TransportKind => transport?.Kind;

    /// <summary>Connects through the requested transport kind, replacing any existing transport.</summary>
    public async Task ConnectAsync(DroneCanTransportKind kind, CancellationToken cancellationToken)
    {
        await DisconnectAsync(cancellationToken).ConfigureAwait(false);
        transport = factory.Create(kind);
        try { await transport.ConnectAsync(cancellationToken).ConfigureAwait(false); }
        catch { await transport.DisposeAsync().ConfigureAwait(false); transport = null; throw; }
    }

    /// <summary>Discovers the nodes on the bus.</summary>
    public Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken ct) => Required().DiscoverAsync(ct);
    /// <summary>Reads every parameter of a node.</summary>
    public Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken ct) => Required().ReadParametersAsync(nodeId, ct);
    /// <summary>Writes one parameter of a node.</summary>
    public Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken ct) => Required().WriteParameterAsync(nodeId, name, value, ct);
    /// <summary>Restarts a node.</summary>
    public Task RestartNodeAsync(byte nodeId, CancellationToken ct) => Required().RestartNodeAsync(nodeId, ct);
    /// <summary>Disconnects and releases the current transport.</summary>
    public async Task DisconnectAsync(CancellationToken ct)
    {
        var current = Interlocked.Exchange(ref transport, null);
        if (current is null) return;
        try { await current.DisconnectAsync(ct).ConfigureAwait(false); }
        finally { await current.DisposeAsync().ConfigureAwait(false); }
    }
    /// <summary>Disconnects the transport.</summary>
    public ValueTask DisposeAsync() => new(DisconnectAsync(CancellationToken.None));
    private IDroneCanTransport Required() => transport is { IsConnected: true } value ? value : throw new InvalidOperationException("Connect a DroneCAN transport first.");
}

/// <summary>Factory used where DroneCAN transports are not available; it returns transports that report the feature as unsupported.</summary>
public sealed class UnsupportedDroneCanTransportFactory : IDroneCanTransportFactory
{
    /// <summary>Creates a transport that reports the feature as unsupported.</summary>
    public IDroneCanTransport Create(DroneCanTransportKind kind) => new UnsupportedDroneCanTransport(kind);
    private sealed class UnsupportedDroneCanTransport(DroneCanTransportKind kind) : IDroneCanTransport
    {
        public DroneCanTransportKind Kind => kind;
        public bool IsConnected => false;
        public Task ConnectAsync(CancellationToken ct) => throw new NotSupportedException($"The {kind} adapter is not installed on this platform.");
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken ct) => throw new InvalidOperationException();
        public Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte id, CancellationToken ct) => throw new InvalidOperationException();
        public Task WriteParameterAsync(byte id, string name, object value, CancellationToken ct) => throw new InvalidOperationException();
        public Task RestartNodeAsync(byte id, CancellationToken ct) => throw new InvalidOperationException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Where a Cube firmware image came from.</summary>
public enum FirmwareSourceKind
{
    /// <summary>A file chosen by the user.</summary>
    Local,
    /// <summary>An image downloaded from the official firmware source.</summary>
    Official,
}
/// <summary>A Cube firmware image ready to send to the device.</summary>
/// <param name="Name">The image display name.</param>
/// <param name="Source">Where the image came from.</param>
/// <param name="Data">The raw image bytes.</param>
public sealed record CubeFirmwareImage(string Name, FirmwareSourceKind Source, byte[] Data)
{
    /// <summary>Gets the CRC-32 of the image data.</summary>
    public uint Crc32 => CubeFirmwareCodec.Crc32(Data);
}
/// <summary>One chunk of a firmware image.</summary>
/// <param name="Offset">The byte offset of the chunk within the image.</param>
/// <param name="Data">The chunk bytes.</param>
public sealed record CubeFirmwareChunk(uint Offset, byte[] Data);
/// <summary>Splits Cube firmware images into chunks and computes their CRC.</summary>
public static class CubeFirmwareCodec
{
    /// <summary>The maximum number of data bytes in one chunk.</summary>
    public const int ChunkSize = 253;
    /// <summary>Computes the CRC-32 of the data.</summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
        }
        return ~crc;
    }
    /// <summary>Splits an image into chunks of at most <see cref="ChunkSize"/> bytes.</summary>
    public static IReadOnlyList<CubeFirmwareChunk> Chunk(ReadOnlySpan<byte> data)
    {
        var chunks = new List<CubeFirmwareChunk>();
        for (var offset = 0; offset < data.Length; offset += ChunkSize)
            chunks.Add(new CubeFirmwareChunk((uint)offset, data.Slice(offset, Math.Min(ChunkSize, data.Length - offset)).ToArray()));
        return chunks;
    }
}

/// <summary>Identifies a MAVLink component.</summary>
/// <param name="SystemId">The MAVLink system identifier.</param>
/// <param name="ComponentId">The MAVLink component identifier.</param>
public sealed record ComponentTarget(byte SystemId, byte ComponentId);
/// <summary>Reads and writes parameters of a MAVLink component.</summary>
public interface IComponentParameterService
{
    /// <summary>Reads all parameters of the component.</summary>
    Task<IReadOnlyDictionary<string, float>> ReadAsync(ComponentTarget target, CancellationToken cancellationToken);
    /// <summary>Writes one parameter of the component.</summary>
    Task WriteAsync(ComponentTarget target, string name, float value, CancellationToken cancellationToken);
}

/// <summary>Encodes and decodes strings packed into float parameter values.</summary>
public static class PackedParameterStringCodec
{
    /// <summary>Packs a string into floats of the given byte length.</summary>
    public static float[] Encode(string value, int byteLength = 16)
    {
        var bytes = new byte[byteLength];
        var encoded = Encoding.UTF8.GetBytes(value);
        encoded.AsSpan(0, Math.Min(encoded.Length, bytes.Length)).CopyTo(bytes);
        return Enumerable.Range(0, byteLength / 4).Select(i => BitConverter.Int32BitsToSingle(BitConverter.ToInt32(bytes, i * 4))).ToArray();
    }
    /// <summary>Unpacks a string from packed float values.</summary>
    public static string Decode(IEnumerable<float> values)
    {
        var bytes = values.SelectMany(value => BitConverter.GetBytes(BitConverter.SingleToInt32Bits(value))).ToArray();
        var end = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
    }
    /// <summary>Replaces the value of a password-like parameter with a placeholder.</summary>
    public static string Redact(string name, string value) => name.Contains("PASS", StringComparison.OrdinalIgnoreCase) ? "<redacted>" : value;
}
