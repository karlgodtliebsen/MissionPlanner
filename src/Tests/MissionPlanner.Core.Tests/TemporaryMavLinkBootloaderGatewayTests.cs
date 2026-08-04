using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

public sealed class TemporaryMavLinkBootloaderGatewayTests
{
    [Theory]
    [InlineData(MavResult.Accepted, true)]
    [InlineData(MavResult.InProgress, true)]
    [InlineData(MavResult.Denied, false)]
    [InlineData(MavResult.Unsupported, false)]
    public async Task UsesIsolatedStreamAndMapsRebootAcknowledgement(MavResult ack, bool expected)
    {
        var stream = new ScriptedStream([1], [2]);
        var factory = new FakePortFactory(stream);
        var encoder = new FakeEncoder();
        var gateway = new TemporaryMavLinkBootloaderGateway(
            factory,
            new MarkerParser(),
            new MarkerDecoder(ack),
            encoder,
            Options.Create(new FirmwareOptions { TemporaryMavLinkHeartbeatTimeout = TimeSpan.FromMilliseconds(100), TemporaryMavLinkCommandAckTimeout = TimeSpan.FromMilliseconds(100) }),
            NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);

        var result = await gateway.RebootToBootloaderAsync(
            new SerialDeviceDescriptor("COM7"),
            TestContext.Current.CancellationToken);

        result.Should().Be(expected);
        factory.PortDisposed.Should().BeTrue();
        encoder.Parameters.Should().NotBeNull();
        encoder.Parameters![0].Should().Be((float)RebootShutdownAction.RebootToBootloader);
        stream.Written.ToArray().Should().Equal(9);
    }

    [Fact]
    public async Task MissingAcknowledgementStillReleasesPortForDiscovery()
    {
        var stream = new ScriptedStream([1]);
        var factory = new FakePortFactory(stream);
        var gateway = new TemporaryMavLinkBootloaderGateway(
            factory,
            new MarkerParser(),
            new MarkerDecoder(MavResult.Accepted),
            new FakeEncoder(),
            Options.Create(new FirmwareOptions { TemporaryMavLinkHeartbeatTimeout = TimeSpan.FromMilliseconds(100), TemporaryMavLinkCommandAckTimeout = TimeSpan.FromMilliseconds(20) }),
            NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);

        var result = await gateway.RebootToBootloaderAsync(
            new SerialDeviceDescriptor("COM7"),
            TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        factory.PortDisposed.Should().BeTrue();
    }

    private sealed class FakePortFactory(Stream stream) : IFirmwareSerialPortFactory
    {
        public bool PortDisposed { get; private set; }

        public Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IFirmwareSerialPort>(new Port(stream, () => PortDisposed = true));
        }

        private sealed class Port(Stream stream, Action dispose) : IFirmwareSerialPort
        {
            public string PortName => "COM7";
            public Stream Stream => stream;
            public bool IsOpen => true;

            public ValueTask DisposeAsync()
            {
                dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class MarkerParser : IMavLinkFrameParser
    {
        public IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, TransportEndPoint endpoint, DateTimeOffset receivedAt)
        {
            return [new MavLinkFrame(1, 1, endpoint, data[0], 0, ReadOnlyMemory<byte>.Empty, data.ToArray(), receivedAt)];
        }

        public void Reset() { }
    }

    private sealed class MarkerDecoder(MavResult result) : IMavLinkMessageDecodeHandler
    {
        public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
        {
            message = frame.MessageId switch
            {
                1 => new HeartbeatMessage(1, 1, frame.EndPoint, 0, 2, 3, 0, 0, 3, frame.ReceivedAt),
                2 => new CommandAckMessage(1, 1, frame.EndPoint, MavLinkCommandIds.PreflightRebootShutdown, (byte)result, frame.ReceivedAt),
                var _ => null
            };
            return message is not null;
        }
    }

    private sealed class FakeEncoder : IMavLinkCommandEncoder
    {
        public IReadOnlyList<float>? Parameters { get; private set; }

        public byte[] EncodeCommandLong(byte targetSystemId, byte targetComponentId, ushort commandId, IReadOnlyList<float> parameters)
        {
            commandId.Should().Be(MavLinkCommandIds.PreflightRebootShutdown);
            Parameters = parameters;
            return [9];
        }

        public byte[] EncodeArmDisarm(byte targetSystemId, byte targetComponentId, bool arm)
        {
            throw new NotSupportedException();
        }

        public byte[] EncodeSetMode(byte vehicleIdSystemId, byte vehicleIdComponentId, uint customMode)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ScriptedStream(params byte[][] reads) : Stream
    {
        private readonly Queue<byte[]> reads = new(reads);
        public MemoryStream Written { get; } = new();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (reads.Count == 0)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            }

            var next = reads.Dequeue();
            next.CopyTo(buffer);
            return next.Length;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Written.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return Written.WriteAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
