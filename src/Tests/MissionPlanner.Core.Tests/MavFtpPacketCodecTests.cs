using FluentAssertions;
using MissionPlanner.MavLink.MavFtp;

namespace MissionPlanner.Core.Tests;

public sealed class MavFtpPacketCodecTests
{
    private readonly MavFtpPacketCodec codec = new();

    [Fact]
    public void RoundTrip_PreservesFields()
    {
        var p = new MavFtpPacket(ushort.MaxValue, 7, MavFtpOpcode.ReadFile, MavFtpOpcode.OpenFileReadOnly, true, 0x12345678, new byte[] { 1, 2, 3 });
        var actual = codec.Decode(codec.Encode(p));
        (actual with { Data = p.Data }).Should().Be(p);
        actual.Data.ToArray().Should().Equal(p.Data.ToArray());
    }

    [Theory]
    [InlineData(MavFtpOpcode.ResetSessions)]
    [InlineData(MavFtpOpcode.OpenFileReadOnly)]
    [InlineData(MavFtpOpcode.ReadFile)]
    [InlineData(MavFtpOpcode.BurstReadFile)]
    [InlineData(MavFtpOpcode.TerminateSession)]
    [InlineData(MavFtpOpcode.ListDirectory)]
    [InlineData(MavFtpOpcode.Ack)]
    [InlineData(MavFtpOpcode.Nak)]
    public void RoundTrip_SupportsOpcodes(MavFtpOpcode opcode)
    {
        codec.Decode(codec.Encode(new MavFtpPacket(1, 0, opcode, 0, false, 0, ReadOnlyMemory<byte>.Empty))).Opcode.Should().Be(opcode);
    }

    [Fact]
    public void MaximumPayload_IsAccepted()
    {
        codec.Encode(new MavFtpPacket(1, 1, MavFtpOpcode.Ack, MavFtpOpcode.ReadFile, false, 0, new byte[239])).Should().HaveCount(251);
    }

    [Fact]
    public void MalformedPayloads_AreRejected()
    {
        Action a = () => codec.Decode(new byte[11]);
        var b = new byte[12];
        b[3] = 128;
        b[4] = 1;
        Action c = () => codec.Decode(b);
        a.Should().Throw<MavFtpProtocolException>();
        c.Should().Throw<MavFtpProtocolException>();
    }

    [Fact]
    public void SequenceWraparound_IsPreserved()
    {
        codec.Decode(codec.Encode(new MavFtpPacket(0, 0, MavFtpOpcode.ResetSessions, 0, false, 0, ReadOnlyMemory<byte>.Empty))).Sequence.Should().Be(0);
        codec.Decode(codec.Encode(new MavFtpPacket(ushort.MaxValue, 0, MavFtpOpcode.ResetSessions, 0, false, 0, ReadOnlyMemory<byte>.Empty))).Sequence.Should().Be(ushort.MaxValue);
    }

    [Fact]
    public void DirectoryCodec_ParsesAndRejectsMalformedEntries()
    {
        MavFtpDirectoryCodec.Decode(System.Text.Encoding.UTF8.GetBytes("Flog.bin\t42\0Dscripts\0S.\0")).Should().HaveCount(3);
        Action a = () => MavFtpDirectoryCodec.Decode("Fbroken\0"u8);
        a.Should().Throw<MavFtpProtocolException>();
    }
}
