using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises classic DroneCAN wire frames and management against a simulated node, without CAN hardware.</summary>
public sealed class DroneCanTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public void TransferCrcIdentifiersToggleAndReplayMatchPublishedV0Layout()
    {
        var payload = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        Assert.Equal(0xDF7F, DroneCanTransferCodec.TransferCrc(0xA7B622F939D1A4D5, payload)); // Independently checked with CRC-HQX.
        var transfer = new DroneCanTransfer(24, 11, 126, 42, true, true, 31, payload, DateTimeOffset.UtcNow);
        var frames = DroneCanTransferCodec.Encode(transfer);
        Assert.Equal(0x180BAAFEu, frames[0].Identifier);
        Assert.Equal(new byte[] { 0x7F, 0xDF, 0, 1, 2, 3, 4, 0x9F }, frames[0].Data);
        Assert.Equal(0x3F, frames[1].Data[^1]); Assert.True((frames[^1].Data[^1] & 0x40) != 0);
        var codec = new DroneCanTransferCodec(); DroneCanTransfer? result = null;
        foreach (var frame in frames) result = codec.Accept(frame, transfer.ObservedAt) ?? result;
        Assert.NotNull(result); Assert.Equal(payload, result.Payload); Assert.Equal((byte)42, result.Destination);
        foreach (var frame in frames) Assert.Null(codec.Accept(frame, transfer.ObservedAt));
        var wrapped = DroneCanTransferCodec.Encode(transfer with { TransferId = 0, Payload = [7] });
        Assert.Equal(new byte[] { 7 }, codec.Accept(wrapped[0], transfer.ObservedAt)!.Payload);
        Assert.Null(codec.Accept(frames[0], transfer.ObservedAt));
    }
    [Fact]
    public void CorruptTruncatedAndOutOfOrderTransfersNeverProduceAResponse()
    {
        var transfer = new DroneCanTransfer(24, 1, 42, 126, true, false, 0, new byte[100], DateTimeOffset.UtcNow);
        var frames = DroneCanTransferCodec.Encode(transfer).ToArray(); frames[4].Data[0] ^= 1;
        var codec = new DroneCanTransferCodec(); foreach (var frame in frames) Assert.Null(codec.Accept(frame, transfer.ObservedAt));
        frames = DroneCanTransferCodec.Encode(transfer).ToArray(); codec = new();
        foreach (var frame in frames.Reverse()) Assert.Null(codec.Accept(frame, transfer.ObservedAt));
        codec = new(); Assert.Null(codec.Accept(frames[0], transfer.ObservedAt));
        foreach (var frame in frames.Skip(1)) Assert.Null(codec.Accept(frame, transfer.ObservedAt.AddSeconds(3)));
        Assert.Null(codec.Accept(new(0xFFFFFFFF, [0xC0]), transfer.ObservedAt));
    }
    [Fact]
    public void ParameterCodecPreservesInt64LimitsAndRejectsNonFiniteOrOutOfRangeEdits()
    {
        Assert.Equal(new byte[] { 0x34, 0x90, 65 }, DroneCanManagementCodec.GetSet(0x1234, "A"));
        Assert.Equal(new byte[] { 0, 1, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 65 }, DroneCanManagementCodec.GetSet(0, "A", -2L));
        var parameter = DroneCanManagementCodec.ReadParameter(42, IntegerParameter(long.MaxValue, long.MinValue, long.MaxValue));
        Assert.NotNull(parameter); Assert.Equal(long.MaxValue, parameter.Value); Assert.Equal(long.MinValue, parameter.Minimum);
        Assert.Equal(long.MinValue, DroneCanManagementCodec.ParseValue(parameter, "-9223372036854775808"));
        Assert.Throws<ArgumentException>(() => DroneCanManagementCodec.ValidateValue(parameter, 1));
        var real = new DroneCanParameter(42, "GAIN", 1f, true) { Minimum = 0f, Maximum = 2f };
        Assert.Throws<ArgumentException>(() => DroneCanManagementCodec.ParseValue(real, "NaN"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DroneCanManagementCodec.ParseValue(real, "3"));
        Assert.Throws<ArgumentException>(() => DroneCanManagementCodec.GetSet(0, new string('X', 93)));
        Assert.Null(DroneCanManagementCodec.ReadParameter(42, [0, 0, 0, 0]));
        Assert.Throws<InvalidDataException>(() => DroneCanManagementCodec.ReadParameter(42, [4, 129]));
    }
    [Fact]
    public void SlcanParserOnlyAcceptsBoundedExtendedDataFrames()
    {
        var frame = SlcanFrameLink.Parse("T1801552A3000102ABCD");
        Assert.NotNull(frame); Assert.Equal(0x1801552Au, frame.Identifier); Assert.Equal(new byte[] { 0, 1, 2 }, frame.Data);
        Assert.Null(SlcanFrameLink.Parse("t123100")); Assert.Null(SlcanFrameLink.Parse("F00"));
        Assert.Throws<InvalidDataException>(() => SlcanFrameLink.Parse("TFFFFFFFF0"));
        Assert.Throws<InvalidDataException>(() => SlcanFrameLink.Parse("T1801552A900"));
        Assert.Throws<InvalidDataException>(() => SlcanFrameLink.Parse("T1801552A1GG"));
    }
    [Fact]
    public async Task DiscoveryParameterReadWriteSaveAndRestartUseNodeIdentityAndReadback()
    {
        var link = new NodeLink(); await using var client = Client(link);
        await client.ConnectAsync(Token);
        var node = Assert.Single(await client.DiscoverAsync(Token)); Assert.Equal("org.test.node", node.Name); Assert.Equal("1.2", node.Version); Assert.Equal(32, node.UniqueId.Length);
        var parameter = Assert.Single(await client.ReadParametersAsync(42, Token)); Assert.Equal(5L, parameter.Value);
        await client.WriteParameterAsync(42, "COUNT", 6L, Token); Assert.Equal(6, link.Value); Assert.Equal(1, link.Writes);
        await client.SaveParametersAsync(42, Token); Assert.Equal(1, link.Saves);
        await client.RestartNodeAsync(42, Token); Assert.Equal(1, link.Restarts);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WriteParameterAsync(42, "COUNT", 7L, Token));
        Assert.Contains(client.Trace, frame => frame.Transmitted);
    }
    [Fact]
    public async Task ConcurrentParameterChangeAndNodeReplacementPreventWrites()
    {
        var link = new NodeLink(); await using var client = Client(link); await client.ConnectAsync(Token); await client.DiscoverAsync(Token); await client.ReadParametersAsync(42, Token);
        link.Value = 8;
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WriteParameterAsync(42, "COUNT", 6L, Token)); Assert.Equal(0, link.Writes);
        await client.ReadParametersAsync(42, Token); link.IdentityByte = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WriteParameterAsync(42, "COUNT", 6L, Token)); Assert.Equal(0, link.Writes);
    }
    [Fact]
    public async Task ReadbackMismatchInvalidatesInventoryAndRequiresAnotherRead()
    {
        var link = new NodeLink { IgnoreWrites = true }; await using var client = Client(link); await client.ConnectAsync(Token); await client.DiscoverAsync(Token); await client.ReadParametersAsync(42, Token);
        await Assert.ThrowsAsync<IOException>(() => client.WriteParameterAsync(42, "COUNT", 6L, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WriteParameterAsync(42, "COUNT", 7L, Token)); Assert.Equal(1, link.Writes);
    }
    [Theory]
    [InlineData(300)]
    [InlineData(512)]
    public async Task FirmwareServesOnlySelectedNodeAndConfirmsCompleteContiguousDelivery(int size)
    {
        var link = new NodeLink(); await using var client = Client(link); await client.ConnectAsync(Token); await client.DiscoverAsync(Token);
        var data = Enumerable.Range(0, size).Select(value => (byte)value).ToArray();
        var result = await client.UpdateFirmwareAsync(42, "board.bin", data, true, null, Token);
        Assert.Equal(data, link.Firmware.ToArray()); Assert.Contains("not acknowledged", result); Assert.Equal(size / 256 + 1, link.FileReads);
        Assert.Equal(0, link.Writes); Assert.Equal(0, link.Restarts);
    }
    [Fact]
    public async Task AllocatorDefaultsOffPersistsBeforeReplyAndStopsForAnotherAllocator()
    {
        var link = new NodeLink(); var store = new AllocationStore(); await using var client = Client(link, store);
        await client.ConnectAsync(Token); Assert.False(client.AllocatorEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAllocatorAsync(true, false, Token));
        await client.SetAllocatorAsync(true, true, Token);
        var uid = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        link.Anonymous([1, .. uid[..6]]); var first = await link.AllocationResponses.Reader.ReadAsync(Token); Assert.Equal(7, first.Length);
        link.Anonymous([0, .. uid[6..12]]); var second = await link.AllocationResponses.Reader.ReadAsync(Token); Assert.Equal(13, second.Length);
        link.Anonymous([0, .. uid[12..]]); var final = await link.AllocationResponses.Reader.ReadAsync(Token);
        Assert.Equal(250, final[0]); Assert.Equal(uid, final[1..]); Assert.Equal((byte)125, Assert.Single(store.Values).NodeId);
        link.Emit(new(24, 1, 80, 0, false, false, 0, [2, 1], DateTimeOffset.UtcNow));
        await WaitAsync(() => !client.AllocatorEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAllocatorAsync(true, true, Token));
    }
    [Fact]
    public async Task MaintenanceIdCollisionAndCancellationCloseTheOwnedLink()
    {
        var link = new NodeLink { Collide = true }; await using var client = Client(link);
        await Assert.ThrowsAsync<IOException>(() => client.ConnectAsync(Token)); Assert.True(link.Disposed);
        var second = new NodeLink(); await using var other = Client(second);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token); cancelled.CancelAfter(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => other.ConnectAsync(cancelled.Token)); Assert.True(second.Disposed);
    }
    [Fact]
    public async Task RtcmBridgeFiltersNodeAndChecksumAndRejectsDisabledClientInput()
    {
        var link = new NodeLink(); await using var client = Client(link); await client.ConnectAsync(Token); await client.DiscoverAsync(Token);
        var port = FreePort(); var ready = new BridgeProgress();
        var run = client.RunPassthroughAsync(42, new() { Port = port }, true, ready, Token);
        await ready.Ready.Task.WaitAsync(TimeSpan.FromSeconds(3), Token);
        using var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, port, Token);
        var data = new byte[] { 0xD3, 0, 2, 0x3E, 0xD0, 0, 0, 0 }; var crc = Rtcm3Framer.Crc24Q(data.AsSpan(0, 5));
        data[5] = (byte)(crc >> 16); data[6] = (byte)(crc >> 8); data[7] = (byte)crc;
        link.Emit(new(24, 1062, 20, 0, false, false, 0, [3, .. data], DateTimeOffset.UtcNow));
        link.Emit(new(24, 1062, 42, 0, false, false, 0, [3, .. data[..4]], DateTimeOffset.UtcNow));
        link.Emit(new(24, 1062, 42, 0, false, false, 1, [3, .. data[4..]], DateTimeOffset.UtcNow));
        var received = new byte[data.Length]; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(3000);
        await tcp.GetStream().ReadExactlyAsync(received, timeout.Token); Assert.Equal(data, received);
        await tcp.GetStream().WriteAsync(new byte[] { 1 }, Token);
        await Assert.ThrowsAsync<IOException>(() => run); Assert.DoesNotContain(link.Broadcasts, message => message.TypeId == 1062);
    }
    [Fact]
    public async Task GpsTunnelPreservesPortDefaultAndReleasesLockOnCancellation()
    {
        Assert.Equal(new byte[] { 2, 85, 0xF1, 0, 0x84, 3, 9 }, DroneCanTunnelCodec.Encode(42, -1, true, 230400, [9]));
        var decoded = DroneCanTunnelCodec.Decode(DroneCanTunnelCodec.Encode(126, 2, false, 0, [1, 2]));
        Assert.NotNull(decoded); Assert.Equal(2, decoded.Value.Serial); Assert.Equal((byte)126, decoded.Value.Node);
        var link = new NodeLink(); await using var client = Client(link); await client.ConnectAsync(Token); await client.DiscoverAsync(Token);
        var port = FreePort(); var ready = new BridgeProgress(); using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = client.RunPassthroughAsync(42, new() { Port = port, GpsSerial = true, LockPort = true }, true, ready, stop.Token);
        await ready.Ready.Task.WaitAsync(TimeSpan.FromSeconds(3), Token);
        using var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, port, Token);
        link.Emit(new(24, 3001, 42, 0, false, false, 0, DroneCanTunnelCodec.Encode(126, 2, false, 0, [1, 2, 3]), DateTimeOffset.UtcNow));
        var received = new byte[3]; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(3000);
        await tcp.GetStream().ReadExactlyAsync(received, timeout.Token); Assert.Equal(new byte[] { 1, 2, 3 }, received);
        stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        var packets = link.Broadcasts.Where(message => message.TypeId == 3001).ToArray();
        Assert.True(packets.Length >= 2); Assert.Equal(new byte[] { 2, 85, 0xF1, 0, 0, 0 }, packets[0].Payload);
        Assert.Equal(new byte[] { 2, 85, 0xF0, 0, 0, 0 }, packets[^1].Payload);
    }
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }
    private sealed class BridgeProgress : IProgress<string>
    {
        internal readonly TaskCompletionSource<bool> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Report(string value) { if (value.StartsWith("Listening", StringComparison.Ordinal)) Ready.TrySetResult(true); }
    }
    private static async Task WaitAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(3000);
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }
    private static DroneCanTransport Client(NodeLink link, AllocationStore? store = null) => new(link, DroneCanTransportKind.DirectSlcan, new() { Port = "simulated", AllocationNetwork = "test-bench" }, store ?? new());
    private static byte[] IntegerParameter(long value, long min = 0, long max = 10)
    {
        var bytes = new byte[28]; bytes[0] = 1; BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(1), value); bytes[9] = 0;
        bytes[10] = 1; BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(11), max); bytes[19] = 1; BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(20), min);
        return [.. bytes, .. Encoding.ASCII.GetBytes("COUNT")];
    }
    private sealed class AllocationStore : IDroneCanAllocationStore
    {
        internal IReadOnlyList<DroneCanAllocation> Values = [];
        public Task<IReadOnlyList<DroneCanAllocation>> LoadAsync(string network, CancellationToken token) => Task.FromResult(Values);
        public Task SaveAsync(string network, IReadOnlyList<DroneCanAllocation> values, CancellationToken token) { Values = values; return Task.CompletedTask; }
    }
    private sealed class NodeLink : IDroneCanFrameLink
    {
        private readonly Channel<DroneCanFrame> frames = Channel.CreateUnbounded<DroneCanFrame>();
        private readonly DroneCanTransferCodec input = new();
        private readonly CancellationTokenSource lifetime = new();
        private Task? beacons;
        private string filePath = "";
        private byte fileTid;
        internal bool Collide, Disposed, IgnoreWrites;
        internal long Value = 5;
        internal byte IdentityByte = 1;
        internal int Writes, Saves, Restarts, FileReads;
        internal readonly List<byte> Firmware = [];
        internal readonly ConcurrentQueue<DroneCanTransfer> Broadcasts = new();
        internal readonly Channel<byte[]> AllocationResponses = Channel.CreateUnbounded<byte[]>();
        public Task OpenAsync(CancellationToken token) { beacons = BeaconAsync(); return Task.CompletedTask; }
        private async Task BeaconAsync()
        {
            byte tid = 0;
            try
            {
                while (true)
                {
                    Emit(new(24, 341, Collide ? (byte)126 : (byte)42, 0, false, false, tid, [100, 0, 0, 0, 0, 0, 0], DateTimeOffset.UtcNow));
                    tid = (byte)((tid + 1) & 31); await Task.Delay(250, lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
        public void Validate() { if (Disposed) throw new IOException("Closed."); }
        public async Task<DroneCanFrame> ReadAsync(CancellationToken token) => await frames.Reader.ReadAsync(token);
        internal void Emit(DroneCanTransfer transfer) { foreach (var frame in DroneCanTransferCodec.Encode(transfer)) frames.Writer.TryWrite(frame); }
        internal void Anonymous(byte[] payload) => frames.Writer.TryWrite(new(0x18000100, [.. payload, 0xC0]));
        public Task SendAsync(DroneCanFrame frame, CancellationToken token)
        {
            var transfer = input.Accept(frame, DateTimeOffset.UtcNow); if (transfer is null) return Task.CompletedTask;
            if (!transfer.IsService) { Broadcasts.Enqueue(transfer); if (transfer.TypeId == 1) AllocationResponses.Writer.TryWrite(transfer.Payload); return Task.CompletedTask; }
            if (!transfer.IsRequest)
            {
                if (transfer.TypeId == 48)
                {
                    Assert.Equal(new byte[] { 0, 0 }, transfer.Payload[..2]); Firmware.AddRange(transfer.Payload[2..]);
                    if (transfer.Payload.Length == 258) RequestFile();
                }
                return Task.CompletedTask;
            }
            byte[] response;
            switch (transfer.TypeId)
            {
                case 1:
                    response = new byte[41]; response[0] = 100; response[7] = 1; response[8] = 2; response[24] = IdentityByte;
                    response = [.. response, .. Encoding.ASCII.GetBytes("org.test.node")]; break;
                case 11:
                    var payload = transfer.Payload; var index = payload[0] | (payload[1] >> 3) << 8;
                    if ((payload[1] & 7) == 1) { Writes++; if (!IgnoreWrites) Value = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(2)); }
                    response = index == 0 ? IntegerParameter(Value) : [0, 0, 0, 0]; break;
                case 10: Saves++; response = [0, 0, 0, 0, 0, 0, 0x80]; break;
                case 5: Assert.Equal(new byte[] { 0x1E, 0x1B, 0x55, 0xCE, 0xAC }, transfer.Payload); Restarts++; response = [0x80]; break;
                case 40: filePath = Encoding.ASCII.GetString(transfer.Payload[1..]); response = [0]; break;
                default: throw new InvalidOperationException("Unexpected service.");
            }
            Emit(transfer with { Source = 42, Destination = 126, IsRequest = false, Payload = response });
            if (transfer.TypeId == 40) RequestFile();
            return Task.CompletedTask;
        }
        private void RequestFile()
        {
            FileReads++; var offset = new byte[5]; BinaryPrimitives.WriteUInt32LittleEndian(offset, (uint)Firmware.Count);
            Emit(new(24, 48, 42, 126, true, true, fileTid, [.. offset, .. Encoding.ASCII.GetBytes(filePath)], DateTimeOffset.UtcNow)); fileTid = (byte)((fileTid + 1) & 31);
        }
        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel(); if (beacons is not null) await beacons; Disposed = true;
        }
    }
}
