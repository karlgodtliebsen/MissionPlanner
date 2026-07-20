using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Encoding;

/// <summary>
/// Provides the public API for MavLinkMissionEncoder.
/// </summary>
public sealed class MavLinkMissionEncoder(IMavLinkCrcExtraProvider crc) : IMavLinkMissionEncoder
{
    private byte sequence;

    /// <summary>
    /// Provides the public API for EncodeMissionCount.
    /// </summary>
    public byte[] EncodeMissionCount(byte ts, byte tc, ushort count, MavMissionType mt)
    {
        Span<byte> p = stackalloc byte[5];
        BinaryPrimitives.WriteUInt16LittleEndian(p, count);
        p[2] = ts;
        p[3] = tc;
        p[4] = (byte)mt;
        return Build(MessageIds.MissionCount, p);
    }

    /// <summary>
    /// Provides the public API for EncodeMissionRequestList.
    /// </summary>
    public byte[] EncodeMissionRequestList(byte ts, byte tc, MavMissionType mt)
    {
        Span<byte> p = stackalloc byte[3];
        p[0] = ts;
        p[1] = tc;
        p[2] = (byte)mt;
        return Build(MessageIds.MissionRequestList, p);
    }

    /// <summary>
    /// Provides the public API for EncodeMissionRequestInt.
    /// </summary>
    public byte[] EncodeMissionRequestInt(byte ts, byte tc, ushort seq, MavMissionType mt)
    {
        Span<byte> p = stackalloc byte[5];
        BinaryPrimitives.WriteUInt16LittleEndian(p, seq);
        p[2] = ts;
        p[3] = tc;
        p[4] = (byte)mt;
        return Build(MessageIds.MissionRequestInt, p);
    }

    /// <summary>
    /// Provides the public API for EncodeMissionAck.
    /// </summary>
    public byte[] EncodeMissionAck(byte ts, byte tc, byte result, MavMissionType mt)
    {
        Span<byte> p = stackalloc byte[4];
        p[0] = ts;
        p[1] = tc;
        p[2] = result;
        p[3] = (byte)mt;
        return Build(MessageIds.MissionAck, p);
    }

    /// <summary>
    /// Provides the public API for EncodeMissionClearAll.
    /// </summary>
    public byte[] EncodeMissionClearAll(byte ts, byte tc, MavMissionType mt)
    {
        Span<byte> p = stackalloc byte[3];
        p[0] = ts;
        p[1] = tc;
        p[2] = (byte)mt;
        return Build(MessageIds.MissionClearAll, p);
    }

    /// <summary>
    /// Provides the public API for EncodeMissionItemInt.
    /// </summary>
    public byte[] EncodeMissionItemInt(byte ts, byte tc, MavLinkMissionItem i)
    {
        Span<byte> p = stackalloc byte[38];
        WF(p, 0, i.Param1);
        WF(p, 4, i.Param2);
        WF(p, 8, i.Param3);
        WF(p, 12, i.Param4);
        BinaryPrimitives.WriteInt32LittleEndian(p[16..20], i.X);
        BinaryPrimitives.WriteInt32LittleEndian(p[20..24], i.Y);
        WF(p, 24, i.Z);
        BinaryPrimitives.WriteUInt16LittleEndian(p[28..30], i.Sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(p[30..32], i.Command);
        p[32] = ts;
        p[33] = tc;
        p[34] = i.Frame;
        p[35] = i.Current ? (byte)1 : (byte)0;
        p[36] = i.AutoContinue ? (byte)1 : (byte)0;
        p[37] = (byte)i.MissionType;
        return Build(MessageIds.MissionItemInt, p);
    }

    private byte[] Build(uint id,
        ReadOnlySpan<byte> p)
    {
        var b = new byte[12 + p.Length];
        b[0] = 0xFD;
        b[1] = (byte)p.Length;
        b[4] = sequence++;
        b[5] = 255;
        b[6] = 190;
        b[7] = (byte)id;
        b[8] = (byte)(id >> 8);
        b[9] = (byte)(id >> 16);
        p.CopyTo(b.AsSpan(10));
        if (!crc.TryGetCrcExtra(id, out var e))
        {
            throw new InvalidOperationException($"No CRC extra for {id}.");
        }

        var c = MavLinkCrc.Calculate(b.AsSpan(1, 9 + p.Length), e);
        b[10 + p.Length] = (byte)c;
        b[11 + p.Length] = (byte)(c >> 8);
        return b;
    }

    private static void WF(Span<byte> p, int o, float v)
    {
        BinaryPrimitives.WriteInt32LittleEndian(p.Slice(o, 4),
            BitConverter.SingleToInt32Bits(v));
    }
}
