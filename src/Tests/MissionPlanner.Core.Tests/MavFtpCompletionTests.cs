using FluentAssertions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Tests;

public sealed class MavFtpCompletionTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/APM/scripts/../logs", "/APM/logs")]
    [InlineData("APM\\scripts", "/APM/scripts")]
    public void RemotePath_NormalizesProtocolPaths(string input, string expected) => RemotePath.Normalize(input).Should().Be(expected);

    [Fact]
    public void RemotePath_JoinsAndStopsAtRoot()
    {
        RemotePath.Join("/APM", "scripts").Should().Be("/APM/scripts");
        RemotePath.Parent("/APM/scripts").Should().Be("/APM");
        RemotePath.Parent("/").Should().Be("/");
    }

    [Fact]
    public void Sequence_ResponseAndWraparound_AreCentralized()
    {
        MavFtpSequence.FirstResponse(41).Should().Be(42);
        MavFtpSequence.FirstResponse(ushort.MaxValue).Should().Be(0);
        MavFtpSequence.Next(ushort.MaxValue).Should().Be(0);
        MavFtpSequence.IsInResponseWindow(ushort.MaxValue, 0).Should().BeTrue();
        MavFtpSequence.IsInResponseWindow(10, 500).Should().BeFalse();
    }

    [Fact]
    public void FileTransferProtocol_CrcExtra_IsRegisteredAndEncoderCanBuildPacket()
    {
        var provider = new CommonMavLinkCrcExtraProvider();
        provider.TryGetCrcExtra(MessageIds.FileTransferProtocol, out var crcExtra).Should().BeTrue();
        crcExtra.Should().Be(84);

        var encoder = new MavFtpMessageEncoder(provider);
        var packet = encoder.Encode(1, 1, new byte[12]);

        packet.Should().NotBeEmpty();
        packet[7].Should().Be((byte)MessageIds.FileTransferProtocol);
    }
}
