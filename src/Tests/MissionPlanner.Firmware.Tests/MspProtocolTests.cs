using MissionPlanner.Firmware.Betaflight.Protocol;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Tests;

public sealed class MspProtocolTests
{
    [Fact]
    public void ExactV1Vectors()
    {
        Assert.Equal(Convert.FromHexString("244D3C000101"), MspFraming.EncodeRequest(MspCommand.ApiVersion, []));
        Assert.Equal(Convert.FromHexString("244D3C01440144"), MspFraming.EncodeRequest(MspCommand.Reboot, [1]));
    }

    [Fact]
    public void NativeV2ExactVector()
    {
        Assert.Equal(Convert.FromHexString("24583C00091000000B"), MspFraming.EncodeRequest(0x1009, []));
        var frame = Assert.Single(Parse(Convert.FromHexString("24583E00091000000B")));
        Assert.Equal(MspProtocolVersion.V2, frame.Version);
        Assert.Equal(0x1009, frame.Command);
        Assert.Empty(frame.Payload);
    }

    [Theory]
    [InlineData("244D3E030100012E2D", 1, "00012E", false)]
    [InlineData("244D3E000505", 5, "", false)]
    [InlineData("244D21000505", 5, "", true)]
    [InlineData("000D0A244D3E030100012E2D", 1, "00012E", false)]
    [InlineData("244D3E030100012E00244D3E030100012E2D", 1, "00012E", false)]
    public void HandAuthoredFragmentedFrames(string hex, ushort command, string payload, bool error)
    {
        var frames = Parse(Convert.FromHexString(hex));
        var frame = Assert.Single(frames);
        Assert.Equal(command, frame.Command);
        Assert.Equal(Convert.FromHexString(payload), frame.Payload);
        Assert.Equal(error, frame.IsError);
    }

    [Fact]
    public void MultipleFramesAndInvalidChecksum()
    {
        Assert.Equal(2, Parse(Convert.FromHexString("244D3E000505244D3E030100012E2D")).Count);
        var parser = new MspFrameParser();
        foreach (var value in Convert.FromHexString("244D3E030100012E00"))
        {
            Assert.Null(parser.Push(value));
        }
        Assert.Equal(MspFailure.ChecksumFailure, parser.Failure);
        Assert.Throws<ArgumentOutOfRangeException>(() => MspFraming.EncodeRequest(1, new byte[1025]));
        Assert.Single(Parse(Convert.FromHexString("24583E000100FFFF244D3E000505")));
    }

    [Fact]
    public async Task ClientIgnoresOtherCommandsAndAcceptsFragmentation()
    {
        await using var port = new FakePort(new ReplyStream(Convert.FromHexString("244D3E000505244D3E030100012E2D"), 1));
        var result = await new BetaflightMspClient(TimeProvider.System).RequestAsync(port, MspCommand.ApiVersion,
            ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(MspFailure.None, result.Failure);
        Assert.True(result.RequestWritten);
        Assert.Equal(Convert.FromHexString("00012E"), result.Frame!.Payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutAndCancellationCloseOwnedIo(bool cancel)
    {
        using var token = new CancellationTokenSource();
        if (cancel)
        {
            token.Cancel();
        }
        await using var port = new FakePort(new ReplyStream([], stall: true));
        var clock = new MissionPlanner.Test.Support.ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var pending = new BetaflightMspClient(clock).RequestAsync(port, 1,
            ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(1), token.Token);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;
        Assert.Equal(cancel ? MspFailure.Cancelled : MspFailure.Timeout, result.Failure);
        Assert.False(port.IsOpen);
    }

    [Theory]
    [InlineData("", MspFailure.Disconnected)]
    [InlineData("244D21000101", MspFailure.MspErrorResponse)]
    public async Task TypedEndpointFailures(string reply, MspFailure expected)
    {
        await using var port = new FakePort(new ReplyStream(Convert.FromHexString(reply)));
        var result = await new BetaflightMspClient(TimeProvider.System).RequestAsync(port, 1,
            ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Failure);
    }

    private static List<MspFrame> Parse(byte[] bytes)
    {
        var parser = new MspFrameParser();
        var frames = new List<MspFrame>();
        foreach (var value in bytes)
        {
            if (parser.Push(value) is { } frame)
            {
                frames.Add(frame);
            }
        }
        return frames;
    }

    [Fact]
    public async Task ExclusiveOpenReportsBusyAndDisposesLateCompletion()
    {
        var clock = new MissionPlanner.Test.Support.ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var factory = new PortFactory();
        var connector = new MspPortConnector(factory, clock);
        factory.Result.SetException(new UnauthorizedAccessException());
        Assert.Equal(MspFailure.PortUnavailableOrBusy,
            (await connector.OpenAsync("fake", TestContext.Current.CancellationToken)).Failure);
        factory = new PortFactory();
        connector = new MspPortConnector(factory, clock);
        var opening = connector.OpenAsync("fake", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(MspFailure.Timeout, (await opening).Failure);
        var late = new FakePort(new ReplyStream([]));
        factory.Result.SetResult(late);
        await late.Closed.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(late.IsOpen);
    }

    private sealed class PortFactory : IFirmwareSerialPortFactory
    {
        public TaskCompletionSource<IFirmwareSerialPort> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default) => Result.Task;
    }

    private sealed class FakePort(Stream stream) : IFirmwareSerialPort
    {
        public string PortName => "fake";
        public Stream Stream => stream;
        public bool IsOpen { get; private set; } = true;
        public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask DisposeAsync()
        {
            IsOpen = false;
            Closed.TrySetResult();
            return stream.DisposeAsync();
        }
    }

    private sealed class ReplyStream(byte[] reply, int chunk = 512, bool stall = false) : MemoryStream
    {
        private int offset;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (stall)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            var count = Math.Min(Math.Min(chunk, buffer.Length), reply.Length - offset);
            reply.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }
    }
}
