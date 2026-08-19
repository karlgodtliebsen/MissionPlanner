using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware;

public enum DroneCanTransportKind { MavLinkTunnel, DirectSlcan }
public enum DroneCanNodeHealth { Unknown, Ok, Warning, Error, Critical }
public enum DroneCanNodeMode { Unknown, Operational, Initialization, Maintenance, Offline }
public sealed record DroneCanNode(byte NodeId, string Name, DroneCanNodeHealth Health, DroneCanNodeMode Mode, string Version, long RxFrames, long TxFrames);
public sealed record DroneCanParameter(byte NodeId, string Name, object Value, bool IsWritable);

public interface IDroneCanTransport : IAsyncDisposable
{
    DroneCanTransportKind Kind { get; }
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken cancellationToken);
    Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken cancellationToken);
    Task RestartNodeAsync(byte nodeId, CancellationToken cancellationToken);
}

public interface IDroneCanTransportFactory
{
    IDroneCanTransport Create(DroneCanTransportKind kind);
}

public interface IDroneCanService : IAsyncDisposable
{
    bool IsConnected { get; }
    DroneCanTransportKind? TransportKind { get; }
    Task ConnectAsync(DroneCanTransportKind kind, CancellationToken cancellationToken);
    Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken cancellationToken);
    Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken cancellationToken);
    Task RestartNodeAsync(byte nodeId, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}

public sealed class DroneCanService(IDroneCanTransportFactory factory) : IDroneCanService
{
    private IDroneCanTransport? transport;
    public bool IsConnected => transport?.IsConnected == true;
    public DroneCanTransportKind? TransportKind => transport?.Kind;

    public async Task ConnectAsync(DroneCanTransportKind kind, CancellationToken cancellationToken)
    {
        await DisconnectAsync(cancellationToken).ConfigureAwait(false);
        transport = factory.Create(kind);
        try { await transport.ConnectAsync(cancellationToken).ConfigureAwait(false); }
        catch { await transport.DisposeAsync().ConfigureAwait(false); transport = null; throw; }
    }

    public Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken ct) => Required().DiscoverAsync(ct);
    public Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken ct) => Required().ReadParametersAsync(nodeId, ct);
    public Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken ct) => Required().WriteParameterAsync(nodeId, name, value, ct);
    public Task RestartNodeAsync(byte nodeId, CancellationToken ct) => Required().RestartNodeAsync(nodeId, ct);
    public async Task DisconnectAsync(CancellationToken ct)
    {
        var current = Interlocked.Exchange(ref transport, null);
        if (current is null) return;
        try { await current.DisconnectAsync(ct).ConfigureAwait(false); }
        finally { await current.DisposeAsync().ConfigureAwait(false); }
    }
    public ValueTask DisposeAsync() => new(DisconnectAsync(CancellationToken.None));
    private IDroneCanTransport Required() => transport is { IsConnected: true } value ? value : throw new InvalidOperationException("Connect a DroneCAN transport first.");
}

public sealed class UnsupportedDroneCanTransportFactory : IDroneCanTransportFactory
{
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

public enum FirmwareSourceKind { Local, Official }
public sealed record CubeFirmwareImage(string Name, FirmwareSourceKind Source, byte[] Data)
{
    public uint Crc32 => CubeFirmwareCodec.Crc32(Data);
}
public sealed record CubeFirmwareChunk(uint Offset, byte[] Data);
public static class CubeFirmwareCodec
{
    public const int ChunkSize = 253;
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
    public static IReadOnlyList<CubeFirmwareChunk> Chunk(ReadOnlySpan<byte> data)
    {
        var chunks = new List<CubeFirmwareChunk>();
        for (var offset = 0; offset < data.Length; offset += ChunkSize)
            chunks.Add(new CubeFirmwareChunk((uint)offset, data.Slice(offset, Math.Min(ChunkSize, data.Length - offset)).ToArray()));
        return chunks;
    }
}

public sealed record ComponentTarget(byte SystemId, byte ComponentId);
public interface IComponentParameterService
{
    Task<IReadOnlyDictionary<string, float>> ReadAsync(ComponentTarget target, CancellationToken cancellationToken);
    Task WriteAsync(ComponentTarget target, string name, float value, CancellationToken cancellationToken);
}

public static class PackedParameterStringCodec
{
    public static float[] Encode(string value, int byteLength = 16)
    {
        var bytes = new byte[byteLength];
        var encoded = Encoding.UTF8.GetBytes(value);
        encoded.AsSpan(0, Math.Min(encoded.Length, bytes.Length)).CopyTo(bytes);
        return Enumerable.Range(0, byteLength / 4).Select(i => BitConverter.Int32BitsToSingle(BitConverter.ToInt32(bytes, i * 4))).ToArray();
    }
    public static string Decode(IEnumerable<float> values)
    {
        var bytes = values.SelectMany(value => BitConverter.GetBytes(BitConverter.SingleToInt32Bits(value))).ToArray();
        var end = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
    }
    public static string Redact(string name, string value) => name.Contains("PASS", StringComparison.OrdinalIgnoreCase) ? "<redacted>" : value;
}
