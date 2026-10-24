using System.Buffers.Binary;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>A classic extended-ID CAN frame; flags such as MAVLink's extended bit are removed at the adapter boundary.</summary>
public sealed record DroneCanFrame(uint Identifier, byte[] Data);
/// <summary>A reconstructed DroneCAN v0 transfer with its routing and replay identity.</summary>
public sealed record DroneCanTransfer(byte Priority, ushort TypeId, byte Source, byte Destination, bool IsService, bool IsRequest,
    byte TransferId, byte[] Payload, DateTimeOffset ObservedAt);

/// <summary>Implements bounded classic-CAN v0 transfer framing, CRC, alternating toggle bits and duplicate suppression.</summary>
public sealed class DroneCanTransferCodec
{
    private readonly Dictionary<uint, Partial> partial = [];
    private readonly Dictionary<uint, (byte Id, DateTimeOffset At)> completed = [];
    /// <summary>Returns the published DSDL signature for the management services supported by this client.</summary>
    public static ulong? Signature(bool service, ushort type) => (service, type) switch
    {
        (false, 1062) => 0x1F56030ECB171501UL,
        (false, 20005) => 0x09F323748C32133AUL,
        (false, 3001) => 0xB138E7EA72A2A2E9UL,
        (false, 341) => 0x0F0868D0C1A7C6F1UL,
        (false, 1) => 0x0B2A812620A11D40UL,
        (true, 1) => 0xEE468A8121C46A9EUL,
        (true, 5) => 0x569E05394A3017F0UL,
        (true, 10) => 0x3B131AC5EB69D2CDUL,
        (true, 11) => 0xA7B622F939D1A4D5UL,
        (true, 40) => 0xB7D725DF72724126UL,
        (true, 48) => 0x8DCDCA939F33F678UL,
        _ => null
    };
    /// <summary>Encodes one transfer in ordered classic-CAN frames, adding a signature-prefixed CRC for multiple frames.</summary>
    public static IReadOnlyList<DroneCanFrame> Encode(DroneCanTransfer transfer)
    {
        if (transfer.Source is 0 or > 127 || transfer.Destination > 127 || transfer.Priority > 31 || transfer.TransferId > 31 || transfer.Payload.Length > 1024 ||
            transfer.IsService && (transfer.TypeId > 255 || transfer.Destination == 0) || !transfer.IsService && transfer.Destination != 0)
            throw new ArgumentException("Invalid DroneCAN v0 transfer route or size.");
        var id = (uint)(transfer.Priority << 24) | transfer.Source;
        id |= transfer.IsService ? (uint)(transfer.TypeId << 16 | (transfer.IsRequest ? 1 : 0) << 15 | transfer.Destination << 8 | 128) : (uint)transfer.TypeId << 8;
        if (transfer.Payload.Length <= 7) return [new(id, [.. transfer.Payload, (byte)(0xC0 | transfer.TransferId)])];
        var signature = Signature(transfer.IsService, transfer.TypeId) ?? throw new NotSupportedException("No DSDL signature registered for this transfer.");
        var payload = new byte[transfer.Payload.Length + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, TransferCrc(signature, transfer.Payload)); transfer.Payload.CopyTo(payload, 2);
        var frames = new List<DroneCanFrame>(); var toggle = false;
        for (var offset = 0; offset < payload.Length; offset += 7)
        {
            var count = Math.Min(7, payload.Length - offset);
            var tail = (byte)(transfer.TransferId | (toggle ? 0x20 : 0) | (offset == 0 ? 0x80 : 0) | (offset + count == payload.Length ? 0x40 : 0));
            frames.Add(new(id, [.. payload.AsSpan(offset, count), tail])); toggle = !toggle;
        }
        return frames;
    }
    /// <summary>Accepts one frame. A file/identity server may explicitly accept same-ID retries of its read-only requests.</summary>
    public DroneCanTransfer? Accept(DroneCanFrame frame, DateTimeOffset now, bool allowReadRequestRetries = false)
    {
        if (frame.Identifier > 0x1FFFFFFF || frame.Data.Length is < 1 or > 8) return null;
        foreach (var key in partial.Where(pair => now < pair.Value.At || now - pair.Value.At > TimeSpan.FromSeconds(2)).Select(pair => pair.Key).ToArray()) partial.Remove(key);
        foreach (var key in completed.Where(pair => now < pair.Value.At || now - pair.Value.At > TimeSpan.FromSeconds(2)).Select(pair => pair.Key).ToArray()) completed.Remove(key);
        var source = (byte)(frame.Identifier & 127); var service = (frame.Identifier & 128) != 0;
        var type = (ushort)(service ? frame.Identifier >> 16 & 255 : source == 0 ? frame.Identifier >> 8 & 3 : frame.Identifier >> 8 & 65535);
        var destination = service ? (byte)(frame.Identifier >> 8 & 127) : (byte)0;
        if (service && (source == 0 || destination == 0)) return null;
        var tail = frame.Data[^1]; var tid = (byte)(tail & 31); var start = (tail & 128) != 0; var end = (tail & 64) != 0; var toggle = (tail & 32) != 0;
        var keyId = frame.Identifier & 0xFFFFFF;
        if (completed.TryGetValue(keyId, out var previous) && source != 0)
        {
            var forward = (tid - previous.Id + 32) & 31;
            var readRetry = allowReadRequestRetries && service && (frame.Identifier & 0x8000) != 0 && type is 1 or 48;
            if (forward == 0 && !readRetry || forward > 15) return null;
        }
        DroneCanTransfer Make(byte[] data) => new((byte)(frame.Identifier >> 24), type, source, destination, service, service && (frame.Identifier & 0x8000) != 0, tid, data, now);
        if (start && end)
        {
            if (toggle) return null;
            partial.Remove(keyId);
            if (source != 0) Remember(keyId, tid, now);
            return Make(frame.Data[..^1]);
        }
        if (source == 0) return null; // Anonymous allocation requests are single-frame only.
        if (start)
        {
            if (toggle || frame.Data.Length != 8 || Signature(service, type) is null) return null;
            if (partial.TryGetValue(keyId, out var same) && same.Id == tid) return null;
            if (partial.Count >= 256) partial.Remove(partial.MinBy(pair => pair.Value.At).Key);
            partial[keyId] = new(tid, now, [.. frame.Data.AsSpan(0, 7)]);
            return null;
        }
        if (!partial.TryGetValue(keyId, out var item) || item.Id != tid || item.Toggle != toggle || !end && frame.Data.Length != 8) return null;
        item.Bytes.AddRange(frame.Data[..^1]); item.Toggle = !item.Toggle;
        if (item.Bytes.Count > 1026) { partial.Remove(keyId); return null; }
        if (!end) return null;
        partial.Remove(keyId);
        var bytes = item.Bytes.ToArray();
        if (bytes.Length < 3 || Signature(service, type) is not { } sig || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != TransferCrc(sig, bytes.AsSpan(2))) return null;
        Remember(keyId, tid, now);
        return Make(bytes[2..]);
    }
    private void Remember(uint key, byte id, DateTimeOffset now)
    {
        if (completed.Count >= 512 && !completed.ContainsKey(key)) completed.Remove(completed.MinBy(pair => pair.Value.At).Key);
        completed[key] = (id, now);
    }
    /// <summary>Computes CRC-16/CCITT-FALSE over little-endian DSDL signature followed by payload.</summary>
    public static ushort TransferCrc(ulong signature, ReadOnlySpan<byte> payload)
    {
        ushort crc = 0xFFFF;
        void Add(byte value)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++) crc = unchecked((ushort)((crc << 1) ^ ((crc & 0x8000) != 0 ? 0x1021 : 0)));
        }
        for (var i = 0; i < 8; i++) Add((byte)(signature >> (8 * i)));
        foreach (var value in payload) Add(value);
        return crc;
    }
    private sealed class Partial(byte id, DateTimeOffset at, List<byte> bytes)
    {
        internal readonly byte Id = id;
        internal readonly DateTimeOffset At = at;
        internal readonly List<byte> Bytes = bytes;
        internal bool Toggle = true;
    }
}
