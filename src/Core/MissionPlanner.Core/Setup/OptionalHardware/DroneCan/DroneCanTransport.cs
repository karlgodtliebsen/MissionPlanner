using System.Buffers.Binary;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Management operations beyond discovery and individual parameter access.</summary>
public interface IDroneCanManagementTransport : IDroneCanTransport
{
    /// <summary>Runs one explicitly enabled localhost RTCM/GPS bridge until cancellation or disconnect.</summary>
    Task RunPassthroughAsync(byte node, DroneCanPassthroughOptions settings, bool confirmed, IProgress<string>? status, CancellationToken token);
    /// <summary>Gets the most recent 1,000 raw frames for inspection/export.</summary>
    IReadOnlyList<DroneCanTrace> Trace { get; }
    /// <summary>Gets whether isolated-bus dynamic allocation is enabled.</summary>
    bool AllocatorEnabled { get; }
    /// <summary>Requests persistent storage of node parameters.</summary>
    Task SaveParametersAsync(byte node, CancellationToken token);
    /// <summary>Transfers a confirmed local image and reports precisely what was verified.</summary>
    Task<string> UpdateFirmwareAsync(byte node, string name, byte[] image, bool confirmed, IProgress<double>? progress, CancellationToken token);
    /// <summary>Enables or disables the isolated-bus allocator.</summary>
    Task SetAllocatorAsync(bool enabled, bool isolatedBusConfirmed, CancellationToken token);
}

/// <summary>A serialized UAVCAN v0 management client with bounded transfer reassembly and captured node identities.</summary>
public sealed partial class DroneCanTransport(IDroneCanFrameLink link, DroneCanTransportKind kind, DroneCanConnectionOptions options,
    IDroneCanAllocationStore allocations) : IDroneCanManagementTransport
{
    private readonly object sync = new();
    private readonly SemaphoreSlim operations = new(1, 1), sends = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly DroneCanTransferCodec codec = new();
    private readonly Dictionary<byte, DroneCanNode> nodes = [];
    private readonly Dictionary<(byte Node, string Name), DroneCanParameter> parameters = [];
    private readonly Dictionary<(byte Node, ushort Type), byte> transferIds = [];
    private readonly Queue<DroneCanTrace> trace = [];
    private readonly Queue<DroneCanTrace> transmitted = [];
    private readonly Dictionary<(uint Id, string Data), DateTimeOffset> echoes = [];
    private readonly byte[] uniqueId = Guid.NewGuid().ToByteArray();
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private Task? receiver, heartbeat, shutdown;
    private Pending? pending;
    private Exception? failure;
    private bool connected;
    private sealed record Pending(byte Node, ushort Type, byte Id, TaskCompletionSource<byte[]> Completion);
    /// <inheritdoc />
    public DroneCanTransportKind Kind => kind;
    /// <inheritdoc />
    public bool IsConnected => connected && failure is null && !lifetime.IsCancellationRequested;
    /// <inheritdoc />
    public IReadOnlyList<DroneCanTrace> Trace { get { lock (sync) return trace.ToArray(); } }
    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken token)
    {
        options.Validate(kind);
        if (receiver is not null) throw new InvalidOperationException("Create a new CAN session after disconnecting.");
        await link.OpenAsync(token).ConfigureAwait(false);
        receiver = ReceiveAsync(lifetime.Token);
        shutdown = CloseWhenStoppedAsync();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            Check(); connected = true;
            heartbeat = HeartbeatAsync(lifetime.Token);
        }
        catch { await DisposeAsync().ConfigureAwait(false); throw; }
    }
    private void Check()
    {
        if (failure is not null) throw new IOException("DroneCAN session stopped; reconnect before continuing.", failure);
        lifetime.Token.ThrowIfCancellationRequested(); link.Validate();
    }
    private DroneCanNode RequireNode(byte id)
    {
        Check();
        lock (sync)
        {
            if (id == options.LocalNodeId || !nodes.TryGetValue(id, out var node) || node.UniqueId.Length != 32 || DateTimeOffset.UtcNow - node.LastSeen > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException("Refresh and select a currently reporting node with verified hardware identity.");
            return node;
        }
    }
    private async Task VerifyNodeAsync(DroneCanNode expected, CancellationToken token)
    {
        var current = await ReadInfoAsync(expected.NodeId, token).ConfigureAwait(false);
        if (expected.UniqueId != current.UniqueId || current.Uptime < expected.Uptime)
            throw new InvalidOperationException("The node changed identity or restarted. Refresh and read its settings again.");
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken token)
    {
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Check(); byte[] ids; lock (sync) ids = nodes.Values.Where(node => DateTimeOffset.UtcNow - node.LastSeen <= TimeSpan.FromSeconds(3)).Select(node => node.NodeId).ToArray();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            foreach (var id in ids)
            {
                try { await ReadInfoAsync(id, timeout.Token).ConfigureAwait(false); }
                catch (TimeoutException) { } // Keep passive status visible; mutation still requires an identified node.
            }
            lock (sync) return nodes.Values.OrderBy(node => node.NodeId).Select(node => DateTimeOffset.UtcNow - node.LastSeen > TimeSpan.FromSeconds(3) ? node with { Mode = DroneCanNodeMode.Offline } : node).ToArray();
        }
        finally { operations.Release(); }
    }
    private async Task<DroneCanNode> ReadInfoAsync(byte id, CancellationToken token)
    {
        var bytes = await RequestAsync(id, 1, [], token).ConfigureAwait(false);
        var info = DroneCanManagementCodec.NodeInfo(bytes);
        lock (sync)
        {
            UpdateStatus(id, bytes.AsSpan(0, 7), DateTimeOffset.UtcNow);
            var node = nodes[id];
            if (node.UniqueId != "" && node.UniqueId != info.UniqueId) InvalidateParameters(id);
            return nodes[id] = node with { Name = info.Name, Version = info.Version, UniqueId = info.UniqueId };
        }
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte nodeId, CancellationToken token)
    {
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var node = RequireNode(nodeId); lock (sync) InvalidateParameters(nodeId);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); timeout.CancelAfter(TimeSpan.FromMinutes(3));
            await VerifyNodeAsync(node, timeout.Token).ConfigureAwait(false);
            var result = new Dictionary<string, DroneCanParameter>(StringComparer.Ordinal);
            for (ushort index = 0; index < 2048; index++)
            {
                var parameter = DroneCanManagementCodec.ReadParameter(nodeId, await RequestAsync(nodeId, 11, DroneCanManagementCodec.GetSet(index), timeout.Token).ConfigureAwait(false));
                if (parameter is null)
                {
                    await VerifyNodeAsync(node, timeout.Token).ConfigureAwait(false);
                    lock (sync) foreach (var value in result.Values) parameters[(nodeId, value.Name)] = value;
                    return result.Values.ToArray();
                }
                if (!result.TryAdd(parameter.Name, parameter)) throw new InvalidDataException("The node repeated a parameter name; inventory is incomplete.");
            }
            throw new InvalidDataException("The node exceeded the 2,048-parameter inventory limit.");
        }
        finally { operations.Release(); }
    }
    /// <inheritdoc />
    public async Task WriteParameterAsync(byte nodeId, string name, object value, CancellationToken token)
    {
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var node = RequireNode(nodeId); DroneCanParameter expected;
            lock (sync) expected = parameters.TryGetValue((nodeId, name), out var item) ? item : throw new InvalidOperationException("Read the node's parameters before writing.");
            DroneCanManagementCodec.ValidateValue(expected, value);
            await VerifyNodeAsync(node, token).ConfigureAwait(false);
            var before = DroneCanManagementCodec.ReadParameter(nodeId, await RequestAsync(nodeId, 11, DroneCanManagementCodec.GetSet(0, name), token).ConfigureAwait(false));
            if (before != expected) throw new InvalidOperationException("Parameter value or metadata changed. Read the node again before writing.");
            var echoed = DroneCanManagementCodec.ReadParameter(nodeId, await RequestAsync(nodeId, 11, DroneCanManagementCodec.GetSet(0, name, value), token).ConfigureAwait(false));
            var actual = DroneCanManagementCodec.ReadParameter(nodeId, await RequestAsync(nodeId, 11, DroneCanManagementCodec.GetSet(0, name), token).ConfigureAwait(false));
            if (echoed?.Name != name || actual?.Name != name || !Equals(echoed.Value, value) || !Equals(actual.Value, value)) throw new IOException("The node's parameter readback differs. Read again; the value may have changed.");
            lock (sync) parameters[(nodeId, name)] = actual;
        }
        catch { lock (sync) InvalidateParameters(nodeId); throw; }
        finally { operations.Release(); }
    }
    /// <inheritdoc />
    public Task RestartNodeAsync(byte nodeId, CancellationToken token) => MaintainAsync(nodeId, true, token);
    /// <inheritdoc />
    public Task SaveParametersAsync(byte node, CancellationToken token) => MaintainAsync(node, false, token);
    private async Task MaintainAsync(byte id, bool restart, CancellationToken token)
    {
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var node = RequireNode(id); await VerifyNodeAsync(node, token).ConfigureAwait(false);
            var response = await RequestAsync(id, restart ? (ushort)5 : (ushort)10, restart ? [0x1E, 0x1B, 0x55, 0xCE, 0xAC] : new byte[7], token).ConfigureAwait(false);
            if (response.Length != (restart ? 1 : 7) || (response[^1] & 0x80) == 0) throw new IOException("The node rejected the maintenance request.");
            if (restart) lock (sync) { InvalidateParameters(id); nodes[id] = nodes[id] with { UniqueId = "", Mode = DroneCanNodeMode.Initialization }; }
        }
        finally { operations.Release(); }
    }
    private void InvalidateParameters(byte id) { foreach (var key in parameters.Keys.Where(key => key.Node == id).ToArray()) parameters.Remove(key); }
    private async Task<byte[]> RequestAsync(byte node, ushort type, byte[] payload, CancellationToken token)
    {
        Check();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        Pending request;
        lock (sync)
        {
            var id = NextId(node, type);
            request = new(node, type, id, new(TaskCreationOptions.RunContinuationsAsynchronously)); pending = request;
        }
        try
        {
            await SendTransferAsync(new(24, type, options.LocalNodeId, node, true, true, request.Id, payload, DateTimeOffset.UtcNow), timeout.Token).ConfigureAwait(false);
            return await request.Completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && !lifetime.IsCancellationRequested) { throw new TimeoutException($"DroneCAN node {node} did not answer service {type}. The request is not replayed."); }
        finally { lock (sync) if (ReferenceEquals(pending, request)) pending = null; }
    }
    private byte NextId(byte node, ushort type)
    {
        var id = transferIds.GetValueOrDefault((node, type)); transferIds[(node, type)] = (byte)((id + 1) & 31); return id;
    }
    private async Task SendTransferAsync(DroneCanTransfer transfer, CancellationToken token)
    {
        await sends.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!transfer.IsService) { lock (sync) transfer = transfer with { TransferId = NextId(0, transfer.TypeId) }; }
            foreach (var frame in DroneCanTransferCodec.Encode(transfer))
            {
                Check(); Record(frame, true); await link.SendAsync(frame, token).ConfigureAwait(false);
                lock (sync) if (transfer.Destination != 0 && nodes.TryGetValue(transfer.Destination, out var node)) nodes[transfer.Destination] = node with { TxFrames = node.TxFrames + 1 };
            }
        }
        finally { sends.Release(); }
    }
    private void Record(DroneCanFrame frame, bool sent)
    {
        lock (sync)
        {
            var item = new DroneCanTrace(DateTimeOffset.UtcNow, sent, frame.Identifier, Convert.ToHexString(frame.Data));
            trace.Enqueue(item); while (trace.Count > 1000) trace.Dequeue();
            if (sent) { transmitted.Enqueue(item); echoes[(item.Identifier, item.Data)] = item.At; }
            while (transmitted.Count > 4096 || transmitted.TryPeek(out var oldest) && item.At - oldest.At > TimeSpan.FromSeconds(3))
            {
                var old = transmitted.Dequeue(); var key = (old.Identifier, old.Data);
                if (echoes.GetValueOrDefault(key) == old.At) echoes.Remove(key);
            }
        }
    }
    private async Task ReceiveAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                var frame = await link.ReadAsync(token).ConfigureAwait(false); var now = DateTimeOffset.UtcNow;
                if ((frame.Identifier & 127) == options.LocalNodeId)
                {
                    bool echo; lock (sync) echo = echoes.TryGetValue((frame.Identifier, Convert.ToHexString(frame.Data)), out var sentAt) && now - sentAt < TimeSpan.FromSeconds(3);
                    if (echo) continue;
                    throw new IOException($"CAN node ID {options.LocalNodeId} is already in use. Choose the other maintenance ID.");
                }
                Record(frame, false);
                var source = (byte)(frame.Identifier & 127);
                lock (sync) if (nodes.TryGetValue(source, out var existing)) nodes[source] = existing with { RxFrames = existing.RxFrames + 1 };
                var transfer = codec.Accept(frame, now, allowReadRequestRetries: true);
                if (transfer is null) continue;
                ReceiveBridge(transfer);
                if (!transfer.IsService && transfer.TypeId == 341 && transfer.Source != 0 && transfer.Payload.Length == 7)
                    lock (sync) UpdateStatus(transfer.Source, transfer.Payload, now);
                if (transfer.IsService && transfer.Destination == options.LocalNodeId)
                {
                    if (!transfer.IsRequest)
                    {
                        lock (sync) if (pending is { } request && request.Node == transfer.Source && request.Type == transfer.TypeId && request.Id == transfer.TransferId) request.Completion.TrySetResult(transfer.Payload);
                    }
                    else await RespondAsync(transfer, token).ConfigureAwait(false);
                }
                else if (!transfer.IsService && transfer.TypeId == 1) await AllocateAsync(transfer, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Fail(ex); }
    }
    private void UpdateStatus(byte id, ReadOnlySpan<byte> data, DateTimeOffset at)
    {
        var uptime = BinaryPrimitives.ReadUInt32LittleEndian(data); var mode = (data[4] >> 3) & 7;
        var node = nodes.GetValueOrDefault(id) ?? new(id, "Unidentified node", DroneCanNodeHealth.Unknown, DroneCanNodeMode.Unknown, "", 1, 0);
        if (uptime < node.Uptime) { InvalidateParameters(id); node = node with { UniqueId = "" }; }
        nodes[id] = node with { Uptime = uptime, Health = (DroneCanNodeHealth)((data[4] >> 6) + 1),
            Mode = mode switch { 0 => DroneCanNodeMode.Operational, 1 => DroneCanNodeMode.Initialization, 2 or 3 => DroneCanNodeMode.Maintenance, 7 => DroneCanNodeMode.Offline, _ => DroneCanNodeMode.Unknown }, LastSeen = at };
    }
    private byte[] OwnStatus()
    {
        var data = new byte[7]; BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)(DateTimeOffset.UtcNow - started).TotalSeconds); return data;
    }
    private async Task HeartbeatAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await SendTransferAsync(new(24, 341, options.LocalNodeId, 0, false, false, 0, OwnStatus(), DateTimeOffset.UtcNow), token).ConfigureAwait(false);
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Fail(ex); }
    }
    private void Fail(Exception ex) { failure = ex; connected = false; allocatorEnabled = false; lock (sync) pending?.Completion.TrySetException(ex); lifetime.Cancel(); }
    private async Task RespondAsync(DroneCanTransfer request, CancellationToken token)
    {
        byte[]? payload = null;
        if (request.TypeId == 1 && request.Payload.Length == 0) payload = [.. OwnStatus(), 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, .. uniqueId, 0, .. Encoding.ASCII.GetBytes("org.missionplanner.nextgen")];
        else if (request.TypeId == 48) payload = ReadFirmware(request);
        if (payload is not null)
        {
            await SendTransferAsync(request with { Source = options.LocalNodeId, Destination = request.Source, IsRequest = false, Payload = payload }, token).ConfigureAwait(false);
            FirmwareResponseSent(request, payload);
        }
    }
    private async Task CloseWhenStoppedAsync()
    {
        try { await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        if (heartbeat is not null) await heartbeat.ConfigureAwait(false);
        if (receiver is not null) await receiver.ConfigureAwait(false);
        await operations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try { await link.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { failure ??= ex; }
        finally { operations.Release(); }
    }
    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken token)
    {
        connected = false; allocatorEnabled = false; lifetime.Cancel();
        if (shutdown is not null) await shutdown.ConfigureAwait(false);
        else await link.DisposeAsync().ConfigureAwait(false);
    }
    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(DisconnectAsync(CancellationToken.None));
}
