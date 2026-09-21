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
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Core.Tests;

public sealed class TemporaryMavLinkBootloaderGatewayTests
{
    [Theory]
    [InlineData(3, FirmwareRuntimeProbeOutcome.Success)]
    [InlineData(12, FirmwareRuntimeProbeOutcome.OtherAutopilot)]
    public async Task DiscoveryProbeRequestsOnlyIdentityAndBanner(byte autopilot, FirmwareRuntimeProbeOutcome outcome)
    {
        var stream = new ScriptedStream([1], [3], [4]);
        var factory = new FakePortFactory(stream);
        var encoder = new FakeEncoder();
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted, autopilot: autopilot),
            encoder, Options.Create(new FirmwareOptions()), NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        var result = await gateway.ProbeAsync(new("COM10"), TestContext.Current.CancellationToken);
        Assert.Equal(outcome, result.Outcome);
        Assert.DoesNotContain(MavLinkCommandIds.PreflightRebootShutdown, encoder.Commands);
        if (autopilot == 3)
        {
            Assert.Equal(new ushort[] { 520, 42428 }, encoder.Commands);
            Assert.Equal(134, result.RunningIdentity!.BoardId);
            Assert.Equal("speedybeef4", result.RunningIdentity.Target);
            Assert.Equal("4.7.1", result.RunningIdentity.Version);
        }
        else
        {
            Assert.Empty(encoder.Commands);
        }
        Assert.True(factory.PortDisposed);
    }

    [Fact]
    public async Task DiscoveryCancellationReleasesNonCancellableNativeRead()
    {
        var factory = new FakePortFactory(new ScriptedStream());
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted),
            new FakeEncoder(), Options.Create(new FirmwareOptions()), NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.ProbeAsync(new("COM10"), cancellation.Token));
        Assert.True(factory.PortDisposed);
    }

    [Theory]
    [InlineData(true, FirmwareRuntimeProbeOutcome.PortBusy)]
    [InlineData(false, FirmwareRuntimeProbeOutcome.TransportError)]
    public async Task DiscoveryPreservesOpenFailureOutcome(bool busy, FirmwareRuntimeProbeOutcome outcome)
    {
        var gateway = new TemporaryMavLinkBootloaderGateway(new FailingPortFactory(busy), new MarkerParser(), new MarkerDecoder(MavResult.Accepted),
            new FakeEncoder(), Options.Create(new FirmwareOptions()), NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        var result = await gateway.ProbeAsync(new("COM10"), TestContext.Current.CancellationToken);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(FirmwareRuntimeKind.Unknown, result.Runtime);
    }

    private sealed class FailingPortFactory(bool busy) : IFirmwareSerialPortFactory
    {
        public Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default)
        {
            throw busy ? new UnauthorizedAccessException() : new IOException();
        }
    }

    [Fact]
    public async Task ArmedHeartbeatPreventsRebootAndReleasesThePort()
    {
        var stream = new ScriptedStream([1]);
        var factory = new FakePortFactory(stream);
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted, 128),
            new FakeEncoder(), Options.Create(new FirmwareOptions()), NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);

        Assert.False(await gateway.RebootToBootloaderAsync(new SerialDeviceDescriptor("COM10"), TestContext.Current.CancellationToken));
        Assert.Equal(0, stream.Written.Length);
        Assert.True(factory.PortDisposed);
    }

    [Fact]
    public async Task RuntimeProbeIdentifiesArduPilotWithoutWritingAnyCommand()
    {
        var stream = new ScriptedStream([1]);
        var factory = new FakePortFactory(stream);
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted),
            new FakeEncoder(), Options.Create(new FirmwareOptions()), NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        var runtime = await gateway.VerifyAsync(new SerialDeviceDescriptor("COM10"), TestContext.Current.CancellationToken);
        Assert.NotNull(runtime);
        Assert.False(runtime.IsArmed);
        Assert.Equal(0, stream.Written.Length);
        Assert.True(factory.PortDisposed);
    }

    [Fact]
    public async Task RuntimeProbeDoesNotClassifyNonArduPilotMavLinkAsArduPilot()
    {
        var stream = new ScriptedStream([1]);
        var factory = new FakePortFactory(stream);
        // A valid MAVLink heartbeat that reports a non-ArduPilot autopilot (e.g. PX4 == 12).
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted, autopilot: 12),
            new FakeEncoder(), Options.Create(new FirmwareOptions { TemporaryMavLinkHeartbeatTimeout = TimeSpan.FromMilliseconds(100) }),
            NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);

        var runtime = await gateway.VerifyAsync(new SerialDeviceDescriptor("COM10"), TestContext.Current.CancellationToken);

        Assert.Null(runtime);
        Assert.Equal(0, stream.Written.Length);
        Assert.True(factory.PortDisposed);
    }

    [Theory]
    [InlineData(MavResult.Accepted)]
    [InlineData(MavResult.InProgress)]
    [InlineData(MavResult.Denied)]
    [InlineData(MavResult.Unsupported)]
    public async Task UsesIsolatedStreamAndReleasesPortAfterSendingReboot(MavResult ack)
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

        result.Should().BeTrue();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SilentApplicationPortIsReleasedOnTimeoutOrCancellation(bool cancel)
    {
        var stream = new ScriptedStream();
        var factory = new FakePortFactory(stream);
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted),
            new FakeEncoder(), Options.Create(new FirmwareOptions { TemporaryMavLinkHeartbeatTimeout = TimeSpan.FromMilliseconds(40) }),
            NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        using var cancellation = new CancellationTokenSource();
        if (cancel)
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(10));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.RebootToBootloaderAsync(new SerialDeviceDescriptor("COM11"), cancellation.Token));
        }
        else
        {
            (await gateway.RebootToBootloaderAsync(new SerialDeviceDescriptor("COM11"), cancellation.Token)).Should().BeFalse();
        }
        factory.PortDisposed.Should().BeTrue();
        stream.Written.Length.Should().Be(0);
    }

    [Fact]
    public async Task DisappearanceWhileSendingRebootStillReleasesTemporaryPort()
    {
        var stream = new ScriptedStream([1]) { DisappearOnWrite = true };
        var factory = new FakePortFactory(stream);
        var gateway = new TemporaryMavLinkBootloaderGateway(factory, new MarkerParser(), new MarkerDecoder(MavResult.Accepted),
            new FakeEncoder(), Options.Create(new FirmwareOptions()), NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        await Assert.ThrowsAsync<IOException>(() => gateway.RebootToBootloaderAsync(new SerialDeviceDescriptor("COM11"), TestContext.Current.CancellationToken));
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
                stream.Dispose();
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

    private sealed class MarkerDecoder(MavResult result, byte baseMode = 0, byte autopilot = 3) : IMavLinkMessageDecodeHandler
    {
        public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
        {
            message = frame.MessageId switch
            {
                1 => new HeartbeatMessage(1, 1, frame.EndPoint, 0, 2, autopilot, baseMode, 0, 3, frame.ReceivedAt),
                2 => new CommandAckMessage(1, 1, frame.EndPoint, MavLinkCommandIds.PreflightRebootShutdown, (byte)result, frame.ReceivedAt),
                3 => new AutopilotVersionMessage(1, 1, frame.EndPoint, 0, 0x040701ff, 0, 0, 134u << 16,
                    [], [], [], 0x1209, 0x5741, 0, [], frame.ReceivedAt),
                4 => new StatusTextMessage(1, 1, frame.EndPoint, MissionPlanner.MavLink.MavSeverity.Info,
                    "speedybeef4 003D0052 32355116 38393232", null, null, frame.ReceivedAt),
                var _ => null
            };
            return message is not null;
        }
    }

    private sealed class FakeEncoder : IMavLinkCommandEncoder
    {
        public List<ushort> Commands { get; } = [];
        public IReadOnlyList<float>? Parameters { get; private set; }

        public byte[] EncodeCommandLong(byte targetSystemId, byte targetComponentId, ushort commandId, IReadOnlyList<float> parameters)
        {
            Commands.Add(commandId);
            Assert.Contains(commandId, new ushort[] { MavLinkCommandIds.PreflightRebootShutdown, 520, 42428 });
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
        private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DisappearOnWrite { get; init; }
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
                // Models SerialPort.BaseStream on Windows, where cancellation may be ignored.
                await closed.Task;
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
            if (DisappearOnWrite)
            {
                throw new IOException("USB device disappeared while sending reboot");
            }
            return Written.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            closed.TrySetResult();
            base.Dispose(disposing);
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
