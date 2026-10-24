using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Explicit localhost bridge settings; input transmission and UART locking default off.</summary>
public sealed record DroneCanPassthroughOptions
{
    /// <summary>Gets the localhost TCP listening port.</summary>
    public int Port { get; init; } = 2001;
    /// <summary>Gets whether to use the node's serial tunnel rather than RTCM streams.</summary>
    public bool GpsSerial { get; init; }
    /// <summary>Gets whether a connected local client may transmit to the CAN bus or serial device.</summary>
    public bool AllowWrites { get; init; }
    /// <summary>Gets the explicit UART number, or -1 for the firmware-selected default.</summary>
    public int SerialId { get; init; } = -1;
    /// <summary>Gets whether to reserve the UART, interrupting its normal driver until released.</summary>
    public bool LockPort { get; init; }
    /// <summary>Gets the requested UART baud, or zero to leave the baud unchanged.</summary>
    public int Baud { get; init; }
    /// <summary>Rejects invalid settings before opening a listener or changing a device.</summary>
    public void Validate()
    {
        if (Port is < 1024 or > 65535 || SerialId is < -1 or > 15 || Baud < 0 || Baud > 2_000_000 || Baud != 0 && (Baud < 9600 || !LockPort))
            throw new ArgumentException("Select a localhost port 1024–65535, UART -1–15 and baud 0 (unchanged), or 9600–2000000 with explicit UART locking.");
    }
}

/// <summary>Serializes the bit-packed UAVCAN v0 Targetted UART tunnel.</summary>
public static class DroneCanTunnelCodec
{
    /// <summary>Encodes GPS_GENERIC, a seven-bit destination, signed five-bit UART, options and little-endian baud.</summary>
    public static byte[] Encode(byte node, int serial, bool locked, int baud, ReadOnlySpan<byte> bytes)
    {
        if (node is 0 or > 127 || serial is < -1 or > 15 || baud is < 0 or > 0xFFFFFF || bytes.Length > 120) throw new ArgumentException("Invalid Targetted tunnel fields.");
        var uart = serial & 31;
        return [2, (byte)(node << 1 | uart >> 4), (byte)((uart & 15) << 4 | (locked ? 1 : 0)), (byte)baud, (byte)(baud >> 8), (byte)(baud >> 16), .. bytes];
    }
    /// <summary>Reads a GPS tunnel packet, preserving the actual UART reported in a default-port response.</summary>
    public static (byte Node, int Serial, byte[] Data)? Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 6 or > 126 || bytes[0] != 2) return null;
        var serial = (bytes[1] & 1) << 4 | bytes[2] >> 4;
        return ((byte)(bytes[1] >> 1), serial >= 16 ? serial - 32 : serial, bytes[6..].ToArray());
    }
}

public sealed partial class DroneCanTransport
{
    private Bridge? bridge;
    private sealed class Bridge(byte node, DroneCanPassthroughOptions options)
    {
        internal readonly byte Node = node;
        internal readonly DroneCanPassthroughOptions Options = options;
        internal readonly Channel<(ushort Type, byte[] Data)> Received = Channel.CreateBounded<(ushort, byte[])>(256);
        internal int? ActualSerial;
        internal bool LockRequested;
    }
    /// <inheritdoc />
    public async Task RunPassthroughAsync(byte node, DroneCanPassthroughOptions settings, bool confirmed, IProgress<string>? status, CancellationToken token)
    {
        settings.Validate(); if (!confirmed) throw new InvalidOperationException("Review and confirm the localhost bridge first.");
        await operations.WaitAsync(token).ConfigureAwait(false);
        var listener = new TcpListener(IPAddress.Loopback, settings.Port);
        using var run = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        Task? watch = null;
        Task<TcpClient>? accept = null;
        Bridge? current = null;
        try
        {
            var expected = RequireNode(node); await VerifyNodeAsync(expected, run.Token).ConfigureAwait(false);
            current = new(node, settings); lock (sync) bridge = current;
            listener.Start(1);
            watch = WatchBridgeAsync(expected, current, run.Token);
            status?.Report($"Listening on 127.0.0.1:{settings.Port} for one client; {(settings.GpsSerial ? "GPS serial" : "RTCM3")} from node {node}. Client writes: {settings.AllowWrites}.");
            accept = listener.AcceptTcpClientAsync(run.Token).AsTask();
            var first = await Task.WhenAny(accept, watch).ConfigureAwait(false);
            if (first == watch) await watch.ConfigureAwait(false);
            using var client = await accept.ConfigureAwait(false); client.NoDelay = true;
            var stream = client.GetStream();
            var incoming = ReceiveTcpAsync(stream, current, run.Token);
            var outgoing = SendTcpAsync(stream, current, run.Token);
            try
            {
                var finished = await Task.WhenAny(incoming, outgoing, watch).ConfigureAwait(false);
                await finished.ConfigureAwait(false);
            }
            finally
            {
                run.Cancel();
                try { await Task.WhenAll(incoming, outgoing).ConfigureAwait(false); }
                catch (OperationCanceledException) when (run.IsCancellationRequested) { }
            }
        }
        finally
        {
            run.Cancel(); listener.Stop();
            lock (sync) bridge = null;
            try
            {
                if (accept is not null)
                {
                    try { (await accept.ConfigureAwait(false)).Dispose(); }
                    catch (Exception ex) when (run.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
                }
                if (watch is not null) { try { await watch.ConfigureAwait(false); } catch (OperationCanceledException) when (run.IsCancellationRequested) { } }
            }
            finally
            {
                try
                {
                    if (current is not null && settings.GpsSerial && current.LockRequested && IsConnected)
                    {
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await BroadcastAsync(3001, DroneCanTunnelCodec.Encode(node, settings.SerialId, false, 0, []), cleanup.Token).ConfigureAwait(false);
                        status?.Report("UART unlock requested. No acknowledgement or prior baud restoration is available; AP_Periph also expires the tunnel after about three seconds without keepalives.");
                    }
                }
                finally { operations.Release(); }
            }
        }
    }
    private async Task WatchBridgeAsync(DroneCanNode expected, Bridge current, CancellationToken token)
    {
        while (true)
        {
            var node = RequireNode(expected.NodeId);
            if (node.UniqueId != expected.UniqueId || node.Uptime < expected.Uptime) throw new InvalidOperationException("The passthrough node changed identity or restarted.");
            if (current.Options.GpsSerial)
            {
                current.LockRequested |= current.Options.LockPort;
                await BroadcastAsync(3001, DroneCanTunnelCodec.Encode(current.Node, current.Options.SerialId, current.Options.LockPort, current.Options.Baud, []), token).ConfigureAwait(false);
            }
            await Task.Delay(1000, token).ConfigureAwait(false);
        }
    }
    private async Task ReceiveTcpAsync(NetworkStream stream, Bridge current, CancellationToken token)
    {
        var input = new byte[120]; var rtcm = new Rtcm3Framer();
        while (true)
        {
            var count = await stream.ReadAsync(input, token).ConfigureAwait(false); if (count == 0) return;
            if (!current.Options.AllowWrites) throw new IOException("Local client sent data while input transmission is disabled; bridge stopped.");
            if (current.Options.GpsSerial)
                await BroadcastAsync(3001, DroneCanTunnelCodec.Encode(current.Node, current.Options.SerialId, current.Options.LockPort, current.Options.Baud, input.AsSpan(0, count)), token).ConfigureAwait(false);
            else foreach (var frame in rtcm.Push(input.AsSpan(0, count)))
                for (var offset = 0; offset < frame.Length; offset += 128)
                    await BroadcastAsync(1062, [3, .. frame.AsSpan(offset, Math.Min(128, frame.Length - offset))], token).ConfigureAwait(false);
        }
    }
    private static async Task SendTcpAsync(NetworkStream stream, Bridge current, CancellationToken token)
    {
        var parsers = new Dictionary<ushort, Rtcm3Framer>();
        while (true)
        {
            var item = await current.Received.Reader.ReadAsync(token).ConfigureAwait(false);
            var outputs = current.Options.GpsSerial ? new[] { item.Data } : (parsers.TryGetValue(item.Type, out var parser) ? parser : parsers[item.Type] = new()).Push(item.Data);
            foreach (var data in outputs)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
                await stream.WriteAsync(data, timeout.Token).ConfigureAwait(false);
            }
        }
    }
    private async Task BroadcastAsync(ushort type, byte[] payload, CancellationToken token)
    {
        await SendTransferAsync(new(24, type, options.LocalNodeId, 0, false, false, 0, payload, DateTimeOffset.UtcNow), token).ConfigureAwait(false);
    }
    private void ReceiveBridge(DroneCanTransfer transfer)
    {
        lock (sync)
        {
            if (bridge is not { } current || current.Node != transfer.Source || transfer.IsService) return;
            byte[]? bytes = null;
            if (current.Options.GpsSerial && transfer.TypeId == 3001 && DroneCanTunnelCodec.Decode(transfer.Payload) is { } tunnel && tunnel.Node == options.LocalNodeId &&
                (current.Options.SerialId == -1 ? current.ActualSerial is null || current.ActualSerial == tunnel.Serial : tunnel.Serial == current.Options.SerialId))
            { current.ActualSerial = tunnel.Serial; bytes = tunnel.Data; }
            else if (!current.Options.GpsSerial)
            {
                if (transfer.TypeId == 1062 && transfer.Payload.Length is > 1 and <= 129 && transfer.Payload[0] == 3) bytes = transfer.Payload[1..];
                else if (transfer.TypeId == 20005 && transfer.Payload.Length is > 0 and <= 300) bytes = transfer.Payload;
            }
            if (bytes is { Length: > 0 } && !current.Received.Writer.TryWrite((transfer.TypeId, bytes))) current.Received.Writer.TryComplete(new IOException("Passthrough receive overflow; client cannot keep up."));
        }
    }
}
