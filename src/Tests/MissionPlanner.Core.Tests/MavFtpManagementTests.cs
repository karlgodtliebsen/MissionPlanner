using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises management transactions through the real packet codec and response dispatcher.</summary>
public sealed class MavFtpManagementTests
{
    [Fact]
    public async Task UploadUsesAllocatedSessionOffsetsAndAlwaysTerminates()
    {
        using var fixture = new Fixture { RemoteCrc = 0x6CD5691Au };
        var bytes = Enumerable.Range(0, 550).Select(number => (byte)number).ToArray();
        using var stream = new MemoryStream(bytes);
        await fixture.Client.UploadFileAsync(fixture.Target, "/test.bin", stream, cancellationToken: TestContext.Current.CancellationToken);
        var writes = fixture.Requests.Where(packet => packet.Opcode == MavFtpOpcode.WriteFile).ToArray();
        Assert.Equal(new uint[] { 0, 239, 478 }, writes.Select(packet => packet.Offset));
        Assert.All(writes, packet => Assert.Equal(7, packet.Session));
        Assert.Equal(bytes, writes.SelectMany(packet => packet.Data.ToArray()).ToArray());
        Assert.Equal(MavFtpOpcode.TerminateSession, fixture.Requests[^2].Opcode);
        Assert.Equal(MavFtpOpcode.CalculateFileCrc32, fixture.Requests[^1].Opcode);
    }

    [Theory]
    [InlineData("123456789", 0x2DFD2D88u)]
    [InlineData("", 0u)]
    public async Task UploadChecksIndependentCrcVectors(string content, uint crc)
    {
        using var fixture = new Fixture { RemoteCrc = crc };
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(content));
        await fixture.Client.UploadFileAsync(fixture.Target, "/vector", source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(MavFtpOpcode.CalculateFileCrc32, fixture.Requests[^1].Opcode);
    }

    [Fact]
    public async Task UploadRejectsCorruptionAfterClosingAndDoesNotDeleteTheFile()
    {
        using var fixture = new Fixture { RemoteCrc = 1 };
        using var source = new MemoryStream(Encoding.ASCII.GetBytes("123456789"));
        await Assert.ThrowsAsync<MavFtpProtocolException>(() => fixture.Client.UploadFileAsync(fixture.Target, "/bad", source,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(MavFtpOpcode.TerminateSession, fixture.Requests[^2].Opcode);
        Assert.DoesNotContain(fixture.Requests, packet => packet.Opcode == MavFtpOpcode.RemoveFile);
    }

    [Fact]
    public async Task InvalidPathsAreRejectedBeforeSending()
    {
        using var fixture = new Fixture();
        using var source = new MemoryStream([1]);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Client.UploadFileAsync(fixture.Target, "/a\0b", source, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Client.UploadFileAsync(fixture.Target, "/" + new string('a', 238), source, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Requests);
    }

    [Fact]
    public async Task RefusedWriteCleansUpWithoutSendingRemainingChunks()
    {
        using var fixture = new Fixture { FailWrites = true };
        using var stream = new MemoryStream(new byte[500]);
        await Assert.ThrowsAsync<MavFtpRemoteException>(() => fixture.Client.UploadFileAsync(fixture.Target, "/test", stream,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(fixture.Requests, packet => packet.Opcode == MavFtpOpcode.WriteFile);
        Assert.Equal(MavFtpOpcode.TerminateSession, fixture.Requests[^1].Opcode);
    }

    [Fact]
    public async Task ExistingFileIsNeverTruncatedByDefault()
    {
        using var fixture = new Fixture { Exists = true };
        using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<IOException>(() => fixture.Client.UploadFileAsync(fixture.Target, "/exists", stream,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.DoesNotContain(fixture.Requests, packet => packet.Opcode is MavFtpOpcode.CreateFile or MavFtpOpcode.WriteFile);
    }

    [Fact]
    public async Task RenameEncodesBothPathsAndCrcUsesLittleEndian()
    {
        using var fixture = new Fixture();
        await fixture.Client.RenameAsync(fixture.Target, "/old", "/new", TestContext.Current.CancellationToken);
        Assert.Equal("/old\0/new\0", Encoding.UTF8.GetString(fixture.Requests[^1].Data.Span));
        Assert.Equal(0x12345678u, await fixture.Client.CalculateCrc32Async(fixture.Target, "/new", TestContext.Current.CancellationToken));
        await fixture.Client.CreateDirectoryAsync(fixture.Target, "/dir", TestContext.Current.CancellationToken);
        Assert.Equal(MavFtpOpcode.CreateDirectory, fixture.Requests[^1].Opcode);
        await fixture.Client.RemoveAsync(fixture.Target, "/dir", true, TestContext.Current.CancellationToken);
        Assert.Equal(MavFtpOpcode.RemoveDirectory, fixture.Requests[^1].Opcode);
    }

    [Fact]
    public async Task ReadRangeHonorsOffsetAndAppendPreservesExistingBytes()
    {
        using var fixture = new Fixture { Exists = true, Contents = Encoding.UTF8.GetBytes("existing console output") };
        var range = await fixture.Client.ReadRangeAsync(fixture.Target, "/repl/out", 9, 7, TestContext.Current.CancellationToken);
        Assert.Equal(23u, range.Size);
        Assert.Equal("console", Encoding.UTF8.GetString(range.Data));
        await fixture.Client.AppendAsync(fixture.Target, "/repl/in", Encoding.UTF8.GetBytes("print(1)\n"), TestContext.Current.CancellationToken);
        var write = Assert.Single(fixture.Requests, p => p.Opcode == MavFtpOpcode.WriteFile);
        Assert.Equal(23u, write.Offset);
        Assert.Equal("print(1)\n", Encoding.UTF8.GetString(write.Data.Span));
        Assert.DoesNotContain(fixture.Requests, p => p.Opcode == MavFtpOpcode.CreateFile);
        Assert.Equal(MavFtpOpcode.TerminateSession, fixture.Requests[^1].Opcode);
    }

    [Fact]
    public async Task AppendRefusesMissingFileBeforeOpeningForWrite()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<MavFtpRemoteException>(() => fixture.Client.AppendAsync(fixture.Target, "/repl/in", new byte[] { 1 }, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(fixture.Requests, p => p.Opcode is MavFtpOpcode.OpenFileWriteOnly or MavFtpOpcode.CreateFile);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MavFtpResponseDispatcher dispatcher;
        public MavFtpTarget Target { get; } = new(1, 1, new TransportEndPoint("test"));
        public List<MavFtpPacket> Requests { get; } = [];
        public MavFtpClient Client { get; }
        public bool Exists { get; init; }
        public uint RemoteCrc { get; init; } = 0x12345678;
        public bool FailWrites { get; init; }
        public byte[] Contents { get; init; } = [];

        public Fixture()
        {
            var codec = new MavFtpPacketCodec();
            var hub = new EventHub(NullLogger<EventHub>.Instance);
            var options = Options.Create(new MavFtpOptions());
            dispatcher = new(hub, codec, options, NullLogger<MavFtpResponseDispatcher>.Instance);
            var connection = Substitute.For<IMavLinkConnection>();
            connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var request = codec.Decode(call.Arg<ReadOnlyMemory<byte>>().Span);
                Requests.Add(request);
                byte[] data = [];
                var nak = false;
                if (request.Opcode is MavFtpOpcode.OpenFileReadOnly or MavFtpOpcode.OpenFileWriteOnly)
                {
                    nak = !Exists;
                    data = nak ? [(byte)MavFtpNakError.FileNotFound] : new byte[4];
                    if (!nak) BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)Contents.Length);
                }
                if (request.Opcode == MavFtpOpcode.ReadFile) data = Contents.Skip((int)request.Offset).Take(request.Data.Length).ToArray();
                if (request.Opcode == MavFtpOpcode.WriteFile && FailWrites)
                {
                    nak = true;
                    data = [(byte)MavFtpNakError.FileProtected];
                }
                if (request.Opcode == MavFtpOpcode.CalculateFileCrc32)
                {
                    data = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(data, RemoteCrc);
                }
                var session = request.Opcode is MavFtpOpcode.CreateFile or MavFtpOpcode.OpenFileReadOnly or MavFtpOpcode.OpenFileWriteOnly ? (byte)7 : request.Session;
                var response = codec.Encode(new((ushort)(request.Sequence + 1), session, nak ? MavFtpOpcode.Nak : MavFtpOpcode.Ack,
                    request.Opcode, false, request.Offset, data));
                return new ValueTask(hub.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage,
                    new FileTransferProtocolMessage(1, 1, Target.EndPoint, 0, 0, 0, response, DateTimeOffset.UtcNow), call.Arg<CancellationToken>()));
            });
            Client = new(connection, new PayloadEncoder(), codec, dispatcher, new MavFtpSequenceStore(), options, NullLogger<MavFtpClient>.Instance);
        }

        public void Dispose() => dispatcher.Dispose();
    }

    private sealed class PayloadEncoder : IMavFtpMessageEncoder
    {
        public byte[] Encode(byte targetSystem, byte targetComponent, ReadOnlySpan<byte> ftpPayload) => ftpPayload.ToArray();
    }
}
