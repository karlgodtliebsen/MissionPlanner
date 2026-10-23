using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Reassembles bounded, complete RAW8 images from the PX4Flow image stream.</summary>
public sealed class PX4FlowImageAssembler
{
    private DataTransmissionHandshakeMessage? header;
    private byte[] pixels = [];
    private bool[] received = [];
    private int count;

    /// <summary>Discards an incomplete frame.</summary>
    public void Reset() { header = null; pixels = []; received = []; count = 0; }

    /// <summary>Accepts a packet; returns a frame only after every packet has arrived.</summary>
    public PX4FlowImage? Accept(MavLinkMessage message)
    {
        if (message is DataTransmissionHandshakeMessage next)
        {
            Reset();
            if (next.Type != (byte)MavlinkDataStreamType.MavlinkDataStreamImgRaw8u ||
                next.Width == 0 || next.Height == 0 || next.Size > 1024 * 1024 ||
                next.Size != (uint)next.Width * next.Height || next.Payload is 0 or > 253 ||
                next.Packets != (next.Size + next.Payload - 1) / next.Payload)
                return null;
            header = next;
            pixels = new byte[next.Size];
            received = new bool[next.Packets];
            return null;
        }
        if (header is not { } h || message is not EncapsulatedDataMessage packet ||
            packet.SystemId != h.SystemId || packet.ComponentId != h.ComponentId ||
            !packet.EndPoint.Equals(h.EndPoint) || packet.ReceivedAt < h.ReceivedAt)
            return null;
        if (packet.ReceivedAt - h.ReceivedAt > TimeSpan.FromSeconds(3)) { Reset(); return null; }
        if (packet.Seqnr >= received.Length || received[packet.Seqnr]) return null;
        var offset = packet.Seqnr * h.Payload;
        var length = Math.Min(h.Payload, pixels.Length - offset);
        if (packet.Data.Length < length) return null;
        packet.Data.AsSpan(0, length).CopyTo(pixels.AsSpan(offset));
        received[packet.Seqnr] = true;
        if (++count != received.Length) return null;
        var frame = new PX4FlowImage(h.Width, h.Height, pixels);
        Reset();
        return frame;
    }
}

/// <summary>A complete eight-bit grayscale sensor image.</summary>
public sealed record PX4FlowImage(int Width, int Height, byte[] Pixels);
