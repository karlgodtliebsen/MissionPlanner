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

/// <summary>
/// Provides the public API for MavFtpCompletionTests.
/// </summary>
public sealed class MavFtpCompletionTests
{
    /// <summary>
    /// Provides the public API for RemotePath_NormalizesProtocolPaths.
    /// </summary>
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/APM/scripts/../logs", "/APM/logs")]
    [InlineData("APM\\scripts", "/APM/scripts")]
    public void RemotePath_NormalizesProtocolPaths(string input, string expected)
    {
        RemotePath.Normalize(input).Should().Be(expected);
    }

    /// <summary>
    /// Provides the public API for RemotePath_JoinsAndStopsAtRoot.
    /// </summary>
    [Fact]
    public void RemotePath_JoinsAndStopsAtRoot()
    {
        RemotePath.Join("/APM", "scripts").Should().Be("/APM/scripts");
        RemotePath.Parent("/APM/scripts").Should().Be("/APM");
        RemotePath.Parent("/").Should().Be("/");
    }

    /// <summary>
    /// Provides the public API for Sequence_ResponseAndWraparound_AreCentralized.
    /// </summary>
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

    /// <summary>
    /// Provides the public API for TransportEndPoint_UsesValueEquality_ForEquivalentUdpEndpoints.
    /// </summary>
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

    /// <summary>
    /// Provides the public API for Sequence_NextRequest_FollowsTheLastServerResponse.
    /// </summary>
    [Fact]
    public void Sequence_NextRequest_FollowsTheLastServerResponse()
    {
        var request = (ushort)42;
        var response = MavFtpSequence.FirstResponse(request);
        var nextRequest = MavFtpSequence.Next(response);

        response.Should().Be(43);
        nextRequest.Should().Be(44);
    }

    /// <summary>
    /// Provides the public API for FileTransferProtocol_CrcExtra_IsRegisteredAndEncoderCanBuildPacket.
    /// </summary>
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

    /// <summary>
    /// Provides the public API for MavFtpClient_Dispose_DoesNotDisposeSharedDispatcherOrSessionConnection.
    /// </summary>
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
            new MavFtpSequenceStore(),
            Options.Create(new MavFtpOptions()),
            NullLogger<MavFtpClient>.Instance);

        await client.DisposeAsync();

        dispatcher.DidNotReceive().Dispose();
        await connection.DidNotReceive().DisposeAsync();
    }

    /// <summary>
    /// Provides the public API for SequenceStore_PersistsAcrossClientLifetimesAndWraps.
    /// </summary>
    [Fact]
    public void SequenceStore_PersistsAcrossClientLifetimesAndWraps()
    {
        var store = new MavFtpSequenceStore();
        var target = new MavFtpTarget(1, 1, new TransportEndPoint("udp", "127.0.0.1", 14550));

        store.GetNextRequest(target).Should().Be(0);

        store.ObserveResponse(target, 1);
        store.GetNextRequest(target).Should().Be(2);

        store.ObserveResponse(target, ushort.MaxValue);
        store.GetNextRequest(target).Should().Be(0);
    }
}
