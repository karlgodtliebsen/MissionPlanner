using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class MavFtpCompletionTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/APM/scripts/../logs", "/APM/logs")]
    [InlineData("APM\\scripts", "/APM/scripts")]
    public void RemotePath_NormalizesProtocolPaths(string input, string expected)
    {
        RemotePath.Normalize(input).Should().Be(expected);
    }

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
        MavFtpSequence.MatchesSingleResponse(41, 41).Should().BeTrue();
        MavFtpSequence.MatchesSingleResponse(41, 42).Should().BeTrue();
        MavFtpSequence.MatchesSingleResponse(41, 43).Should().BeFalse();
        MavFtpSequence.MatchesSingleResponse(ushort.MaxValue, 0).Should().BeTrue();
    }

    [Fact]
    public void TransportEndPoint_UsesValueEquality_ForEquivalentUdpEndpoints()
    {
        var first = new TransportEndPoint("udp", new IPEndPoint(IPAddress.Loopback, 14450));
        var second = new TransportEndPoint("UDP", "127.0.0.1", 14450);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());

        var targets = new HashSet<MavFtpTarget> { new(1, 1, first), new(1, 1, second) };
        targets.Should().ContainSingle();
    }

    [Fact]
    public void Sequence_NextRequest_FollowsTheLastServerResponse()
    {
        var request = (ushort)42;
        var response = MavFtpSequence.FirstResponse(request);
        var nextRequest = MavFtpSequence.Next(response);

        response.Should().Be(43);
        nextRequest.Should().Be(44);
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
        packet[5].Should().Be(254, "MAVFTP must not share MAVProxy's common 255:190 sender identity");
        packet[6].Should().Be(190);
        packet[7].Should().Be((byte)MessageIds.FileTransferProtocol);
    }

    [Fact]
    public async Task MavFtpClient_Dispose_DoesNotDisposeSharedDispatcherOrSessionConnection()
    {
        var connection = Substitute.For<IMavLinkConnection>();
        var dispatcher = Substitute.For<IMavFtpResponseDispatcher>();
        var encoder = Substitute.For<IMavFtpMessageEncoder>();
        var codec = Substitute.For<IMavFtpPacketCodec>();

        var client = new MavFtpClient(
            connection,
            encoder,
            codec,
            dispatcher,
            Options.Create(new MavFtpOptions()),
            NullLogger<MavFtpClient>.Instance);

        await client.DisposeAsync();

        dispatcher.DidNotReceive().Dispose();
        connection.DidNotReceive().DisposeAsync();
    }
}
