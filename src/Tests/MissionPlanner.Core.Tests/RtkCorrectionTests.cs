using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.Core.Tests;

public sealed class RtkCorrectionTests
{
    [Fact]
    public void FramerPreservesPartialInputAndValidatesCrc()
    {
        var frame = Frame(1005, [1, 2, 3, 4]);
        var framer = new Rtcm3Framer();

        framer.Push(frame.AsSpan(0, 4)).Should().BeEmpty();
        var result = framer.Push(frame.AsSpan(4));

        result.Should().ContainSingle().Which.Should().Equal(frame);
        Rtcm3Framer.MessageType(result[0]).Should().Be(1005);
    }

    [Fact]
    public void LargeFrameUsesDeterministicMavlinkFragmentFlags()
    {
        var fragments = GpsRtcmFragmenter.Fragment(new byte[400], 7);

        fragments.Should().HaveCount(3);
        fragments.Select(item => item.Flags).Should().Equal(57, 59, 61);
        fragments.Select(item => item.Data.Length).Should().Equal(180, 180, 40);
    }

    [Fact]
    public void OversizedFrameIsRejectedInsteadOfInterleaved()
    {
        var action = () => GpsRtcmFragmenter.Fragment(new byte[721], 0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static byte[] Frame(int messageType, byte[] tail)
    {
        var payload = new byte[2 + tail.Length];
        payload[0] = (byte)(messageType >> 4);
        payload[1] = (byte)(messageType << 4);
        tail.CopyTo(payload, 2);
        var frame = new byte[payload.Length + 6];
        frame[0] = 0xD3;
        frame[1] = (byte)(payload.Length >> 8);
        frame[2] = (byte)payload.Length;
        payload.CopyTo(frame, 3);
        var crc = Rtcm3Framer.Crc24Q(frame.AsSpan(0, frame.Length - 3));
        frame[^3] = (byte)(crc >> 16); frame[^2] = (byte)(crc >> 8); frame[^1] = (byte)crc;
        return frame;
    }
}
