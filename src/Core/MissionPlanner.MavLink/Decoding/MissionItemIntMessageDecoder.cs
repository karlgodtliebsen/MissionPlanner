using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

public sealed class MissionItemIntMessageDecoder : IMavLinkMessageDecoder
{
    public uint MessageId => MessageIds.MissionItemInt;
    public byte CrcExtra => 38;

    public bool TryDecode(MavLinkFrame f, out MavLinkMessage? m)
    {
        m = null;
        if (f.MessageId != MessageId || f.Payload.Length < 37)
        {
            return false;
        }

        var s = f.Payload.Span;
        m = new MissionItemIntMessage(f.SystemId, f.ComponentId, f.EndPoint, ReadF(s, 0), ReadF(s, 4), ReadF(s, 8), ReadF(s, 12), System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s[16..20]), System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s[20..24]), ReadF(s, 24), MavLinkDecoderHelpers.ReadUInt16OrDefault(s, 28), MavLinkDecoderHelpers.ReadUInt16OrDefault(s, 30), s[32], s[33], s[34], s[35], s[36], s.Length >= 38 ? s[37] : (byte)0, f.ReceivedAt);
        return true;
    }

    private static float ReadF(ReadOnlySpan<byte> s, int o)
    {
        return BitConverter.Int32BitsToSingle(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s.Slice(o, 4)));
    }
}
