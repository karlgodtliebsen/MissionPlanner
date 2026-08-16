using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Protocol;

namespace MissionPlanner.Firmware.Tests;

public sealed class ArduPilotBootloaderClientTests
{
    [Fact]
    public async Task IdentifiesProgramsVerifiesAndRebootsWithFragmentedReplies()
    {
        var image = new byte[] { 1, 2, 3 };
        var package = new ApjFirmwarePackage(50, image, 16);
        var expectedCrc = ArduPilotFirmwareChecksum.Calculate(image, 16);
        var stream = new ScriptedBootloaderStream(command => Reply(command, expectedCrc));
        await using var client = CreateClient(stream);

        var identity = await client.IdentifyAsync(TestContext.Current.CancellationToken);
        await client.EraseAsync(TestContext.Current.CancellationToken);
        await client.ProgramAsync(package, cancellationToken: TestContext.Current.CancellationToken);
        var verification = await client.VerifyAsync(package, TestContext.Current.CancellationToken);
        await client.RebootAsync(TestContext.Current.CancellationToken);

        identity.Should().Be(new BootloaderIdentity(50, 4, 16, 2));
        verification.Succeeded.Should().BeTrue();
        stream.Commands.Should().Contain(command => command[0] == 0x27 && command[1] == 4 && command[command.Length - 2] == 0xff);
        stream.Commands.Last()[0].Should().Be(0x30);
    }

    [Fact]
    public async Task IdentifiesRevisionFiveWithoutProbingUnneededExternalFlash()
    {
        var stream = new ScriptedBootloaderStream(command =>
        {
            if (command[0] == 0x2e)
                return [.. UInt32(4), (byte)'F', (byte)'4', (byte)'0', (byte)'5', 0x12, 0x10];
            if (command[0] == 0x22)
            {
                var value = command[1] switch { 1 => 5u, 2 => 134u, 3 => 1u, 4 => 983040u, var _ => 0u };
                return [.. UInt32(value), 0x12, 0x10];
            }
            return [0x12, 0x10];
        });
        await using var client = CreateClient(stream);

        var identity = await client.IdentifyAsync(TestContext.Current.CancellationToken);

        identity.Should().Be(new BootloaderIdentity(134, 5, 983040, 1, chipDescription: "F405"));
        stream.Commands.Should().NotContain(command => command[0] == 0x22 && command[1] == 6);
    }

    [Fact]
    public async Task RejectsInvalidSyncAfterBoundedRetries()
    {
        var stream = new ScriptedBootloaderStream(_ => new byte[] { 0, 0x10 });
        await using var client = CreateClient(stream);

        var act = async () => await client.IdentifyAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareBootloaderException>();
        stream.Commands.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExactReadTimesOutWhenTransportNeverReplies()
    {
        await using var client = CreateClient(new HangingStream());

        var act = async () => await client.IdentifyAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareBootloaderException>();
    }

    [Fact]
    public async Task DisconnectDuringEraseIsReported()
    {
        var stream = new ScriptedBootloaderStream(command => command[0] == 0x23 ? [] : Reply(command, 0));
        await using var client = CreateClient(stream);
        _ = await client.IdentifyAsync(TestContext.Current.CancellationToken);

        var act = async () => await client.EraseAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareBootloaderException>();
    }

    [Fact]
    public async Task BoardMismatchAndInsufficientFlashAreRejectedBeforeProgramming()
    {
        var stream = new ScriptedBootloaderStream(command => Reply(command, 0));
        await using var client = CreateClient(stream);
        _ = await client.IdentifyAsync(TestContext.Current.CancellationToken);

        var wrongBoard = async () => await client.ProgramAsync(new ApjFirmwarePackage(51, new byte[] { 1 }, 16), cancellationToken: TestContext.Current.CancellationToken);
        var tooLarge = async () => await client.ProgramAsync(new ApjFirmwarePackage(50, new byte[17], 32), cancellationToken: TestContext.Current.CancellationToken);

        await wrongBoard.Should().ThrowAsync<FirmwareCompatibilityException>();
        await tooLarge.Should().ThrowAsync<FirmwareCompatibilityException>();
        stream.Commands.Should().NotContain(command => command[0] == 0x27);
    }

    [Fact]
    public async Task ApprovedBoardMismatchUsesSamePolicyForProgrammingAndVerification()
    {
        var package = new ApjFirmwarePackage(51, new byte[] { 1, 2, 3 }, 16);
        var expectedCrc = ArduPilotFirmwareChecksum.Calculate(package.Image.Span, 16);
        var stream = new ScriptedBootloaderStream(command => Reply(command, expectedCrc));
        await using var client = CreateClient(stream);
        _ = await client.IdentifyAsync(TestContext.Current.CancellationToken);
        var policy = new FirmwareCompatibilityPolicy(AllowBoardIdMismatch: true);

        await client.ProgramAsync(package, policy, cancellationToken: TestContext.Current.CancellationToken);
        var verification = await client.VerifyAsync(package, policy, TestContext.Current.CancellationToken);

        verification.Succeeded.Should().BeTrue();
        stream.Commands.Should().Contain(command => command[0] == 0x27);
    }

    [Fact]
    public async Task ApprovedBoardMismatchStillRejectsInsufficientFlash()
    {
        var stream = new ScriptedBootloaderStream(command => Reply(command, 0));
        await using var client = CreateClient(stream);
        _ = await client.IdentifyAsync(TestContext.Current.CancellationToken);

        var act = async () => await client.ProgramAsync(
            new ApjFirmwarePackage(51, new byte[17], 32),
            new FirmwareCompatibilityPolicy(AllowBoardIdMismatch: true),
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareCompatibilityException>();
        stream.Commands.Should().NotContain(command => command[0] == 0x27);
    }

    [Fact]
    public async Task DisconnectDuringProgrammingIsReported()
    {
        var stream = new ScriptedBootloaderStream(command => command[0] == 0x27 ? [] : Reply(command, 0));
        await using var client = CreateClient(stream);
        _ = await client.IdentifyAsync(TestContext.Current.CancellationToken);

        var act = async () => await client.ProgramAsync(
            new ApjFirmwarePackage(50, new byte[] { 1, 2, 3 }, 16),
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareBootloaderException>();
    }

    [Fact]
    public async Task WrongChecksumNeverReportsSuccess()
    {
        var stream = new ScriptedBootloaderStream(command => Reply(command, 0x12345678));
        await using var client = CreateClient(stream);
        _ = await client.IdentifyAsync(TestContext.Current.CancellationToken);
        var package = new ApjFirmwarePackage(50, new byte[] { 1, 2, 3 }, 16);

        var result = await client.VerifyAsync(package, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
        result.ActualChecksum.Should().Be(0x12345678);
    }

    private static ArduPilotBootloaderClient CreateClient(Stream stream)
    {
        var options = Options.Create(new FirmwareOptions { BootloaderCommandTimeout = TimeSpan.FromMilliseconds(30), BootloaderEraseTimeout = TimeSpan.FromMilliseconds(30), BootloaderSyncAttempts = 2, BootloaderRetryDelay = TimeSpan.Zero });
        return new ArduPilotBootloaderClient(new TestPort(stream), options, TimeProvider.System, NullLogger<ArduPilotBootloaderClient>.Instance);
    }

    private static byte[] Reply(byte[] command, uint checksum)
    {
        if (command[0] == 0x22)
        {
            var value = command[1] switch { 1 => 4u, 2 => 50u, 3 => 2u, 4 => 16u, var _ => 0u };
            return [.. UInt32(value), 0x12, 0x10];
        }

        if (command[0] == 0x29)
        {
            return [.. UInt32(checksum), 0x12, 0x10];
        }

        return command[0] == 0x30 ? [] : [0x12, 0x10];
    }

    private static byte[] UInt32(uint value)
    {
        var result = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, value);
        return result;
    }

    private sealed class TestPort(Stream stream) : IFirmwareSerialPort
    {
        public string PortName => "TEST";
        public Stream Stream => stream;
        public bool IsOpen => true;

        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedBootloaderStream(Func<byte[], byte[]> reply) : Stream
    {
        private readonly Queue<byte> input = new();
        public List<byte[]> Commands { get; } = [];

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = buffer.ToArray();
            Commands.Add(command);
            foreach (var value in reply(command))
            {
                input.Enqueue(value);
            }

            await ValueTask.CompletedTask;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            buffer.Span[0] = input.Dequeue();
            return ValueTask.FromResult(1);
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }

    private sealed class HangingStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Models SerialPort.BaseStream on Windows, where cancellation may be ignored.
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
