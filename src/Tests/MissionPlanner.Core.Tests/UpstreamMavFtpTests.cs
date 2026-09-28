using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class UpstreamMavFtpTests
{
    [Fact]
    public void TimedEntriesRetainSizeAndDecodeUtc()
    {
        var entries = MavFtpDirectoryCodec.Decode(Encoding.UTF8.GetBytes("Fflight.bin\t123\t1700000000\0Dlogs\t0\t0\0S\0"), true);
        Assert.Equal(123, entries[0].Size);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), entries[0].ModifiedUtc);
        Assert.Equal("logs", entries[1].Name);
        Assert.Null(entries[1].ModifiedUtc);
        Assert.Equal(MavFtpDirectoryEntryType.Skip, entries[2].Type);
        Assert.Equal("folder\tname", Assert.Single(MavFtpDirectoryCodec.Decode(Encoding.UTF8.GetBytes("Dfolder\tname\0"))).Name);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4294967296")]
    [InlineData("invalid")]
    public void UnknownOrInvalidTimesAreNotInvented(string time)
    {
        Assert.Null(Assert.Single(MavFtpDirectoryCodec.Decode(Encoding.UTF8.GetBytes($"Ftest\t12\t{time}\0"), true)).ModifiedUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListingsNegotiateFallbackAndCountSkippedEntries(bool legacy)
    {
        var endpoint = new TransportEndPoint("test");
        var codec = new MavFtpPacketCodec();
        var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var dispatcher = new MavFtpResponseDispatcher(hub, codec, Options.Create(new MavFtpOptions()), NullLogger<MavFtpResponseDispatcher>.Instance);
        var connection = Substitute.For<IMavLinkConnection>();
        var requests = new List<MavFtpPacket>();
        connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = codec.Decode(call.Arg<ReadOnlyMemory<byte>>().Span);
            requests.Add(request);
            var rejected = legacy && request.Opcode == MavFtpOpcode.ListDirectoryWithTime;
            var end = request.Offset > 0;
            var data = rejected ? new byte[] { (byte)MavFtpNakError.UnknownCommand }
                : end ? new byte[] { (byte)MavFtpNakError.EndOfFile }
                : Encoding.UTF8.GetBytes(legacy ? "Ftest\t12\0S\0" : "Ftest\t12\t1700000000\0S\0");
            var response = codec.Encode(new((ushort)(request.Sequence + 1), 0,
                rejected || end ? MavFtpOpcode.Nak : MavFtpOpcode.Ack, request.Opcode, false, request.Offset, data));
            return new ValueTask(hub.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage,
                new FileTransferProtocolMessage(1, 1, endpoint, 0, 0, 0, response, DateTimeOffset.UtcNow), call.Arg<CancellationToken>()));
        });
        await using var client = new MavFtpClient(connection, new PayloadEncoder(), codec, dispatcher, new MavFtpSequenceStore(),
            Options.Create(new MavFtpOptions()), NullLogger<MavFtpClient>.Instance);
        var files = await client.ListDirectoryAsync(new(1, 1, endpoint), "/", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("test", Assert.Single(files).Name);
        Assert.Equal(!legacy, files[0].ModifiedUtc.HasValue);
        Assert.Equal(2u, requests[^1].Offset);
        Assert.Equal(MavFtpOpcode.ListDirectoryWithTime, requests[0].Opcode);
        Assert.Equal(legacy ? MavFtpOpcode.ListDirectory : MavFtpOpcode.ListDirectoryWithTime, requests[^1].Opcode);
    }

    private sealed class PayloadEncoder : IMavFtpMessageEncoder
    {
        public byte[] Encode(byte targetSystem, byte targetComponent, ReadOnlySpan<byte> ftpPayload) => ftpPayload.ToArray();
    }
}
