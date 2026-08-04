using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Protocol;

/// <summary>Implements the bounded modern ArduPilot serial bootloader conversation.</summary>
public sealed class ArduPilotBootloaderClient(
    IFirmwareSerialPort port,
    IOptions<FirmwareOptions> options,
    TimeProvider timeProvider,
    ILogger<ArduPilotBootloaderClient> logger) : IArduPilotBootloaderClient
{
    private BootloaderIdentity? identity;

    /// <inheritdoc />
    public async Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeAsync(cancellationToken).ConfigureAwait(false);
        var identifyTimeout = options.Value.BootloaderSynchronizationTimeout;
        var revision = checked((int)await GetInfoAsync(ArduPilotBootloaderProtocol.InfoBootloaderRevision, identifyTimeout, cancellationToken).ConfigureAwait(false));
        if (revision is < ArduPilotBootloaderProtocol.MinimumBootloaderRevision or > ArduPilotBootloaderProtocol.MaximumBootloaderRevision)
            throw new FirmwareBootloaderException($"Unsupported bootloader revision {revision}.");
        var boardId = checked((int)await GetInfoAsync(ArduPilotBootloaderProtocol.InfoBoardId, identifyTimeout, cancellationToken).ConfigureAwait(false));
        var boardRevision = checked((int)await GetInfoAsync(ArduPilotBootloaderProtocol.InfoBoardRevision, identifyTimeout, cancellationToken).ConfigureAwait(false));
        var flashSize = await GetInfoAsync(ArduPilotBootloaderProtocol.InfoFlashSize, identifyTimeout, cancellationToken).ConfigureAwait(false);
        var chip = revision >= 5 ? await TryGetChipDescriptionAsync(identifyTimeout, cancellationToken).ConfigureAwait(false) : null;
        // External flash is not required by normal APJ application images. Some revision-five
        // bootloaders accept the info command but never reply, leaving a native Windows serial
        // read pending after the bounded async timeout. Report unavailable capacity so packages
        // that actually require external flash remain conservatively blocked before erase.
        const uint externalSize = 0;
        identity = new BootloaderIdentity(boardId, revision, flashSize, boardRevision, externalSize, chip);
        logger.LogInformation("Identified bootloader board {BoardId}, revision {Revision}, flash {FlashSize} bytes.", boardId, revision, flashSize);
        return identity;
    }

    /// <inheritdoc />
    public async Task EraseAsync(CancellationToken cancellationToken = default)
    {
        RequireIdentity();
        logger.LogInformation("Erasing application flash on {PortName}.", port.PortName);
        await ExecuteStatusCommandAsync([ArduPilotBootloaderProtocol.ChipErase, ArduPilotBootloaderProtocol.EndOfCommand], options.Value.BootloaderEraseTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var device = RequireIdentity();
        ValidatePackage(device, package);
        if (!package.ExternalImage.IsEmpty)
        {
            var size = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)package.ExternalImage.Length));
            await ExecuteStatusCommandAsync([ArduPilotBootloaderProtocol.ExternalErase, .. size, ArduPilotBootloaderProtocol.EndOfCommand], options.Value.BootloaderEraseTimeout, cancellationToken).ConfigureAwait(false);
            await ProgramImageAsync(package.ExternalImage, ArduPilotBootloaderProtocol.ExternalProgramMulti, 0, package.Image.Length + package.ExternalImage.Length, progress, cancellationToken).ConfigureAwait(false);
        }

        await ProgramImageAsync(package.Image, ArduPilotBootloaderProtocol.ProgramMulti, package.ExternalImage.Length, package.Image.Length + package.ExternalImage.Length, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var device = RequireIdentity();
        ValidatePackage(device, package);
        var actual = await GetChecksumAsync(ArduPilotBootloaderProtocol.GetCrc, cancellationToken).ConfigureAwait(false);
        var expected = ArduPilotFirmwareChecksum.Calculate(package.Image.Span, checked((int)device.FlashSize));
        uint? externalActual = null;
        uint? externalExpected = null;
        if (!package.ExternalImage.IsEmpty)
        {
            externalActual = await GetExternalChecksumAsync(package.ExternalImage.Length, cancellationToken).ConfigureAwait(false);
            externalExpected = ArduPilotFirmwareChecksum.Update(0, Pad4(package.ExternalImage.Span));
        }

        var succeeded = actual == expected && externalActual == externalExpected;
        return new FirmwareVerificationResult(succeeded, expected, actual, externalExpected, externalActual);
    }

    /// <inheritdoc />
    public Task RebootAsync(CancellationToken cancellationToken = default) =>
        WriteAsync([ArduPilotBootloaderProtocol.Reboot, ArduPilotBootloaderProtocol.EndOfCommand], options.Value.BootloaderCommandTimeout, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => port.DisposeAsync();

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= options.Value.BootloaderSyncAttempts; attempt++)
        {
            try
            {
                await ExecuteStatusCommandAsync(
                    [ArduPilotBootloaderProtocol.GetSync, ArduPilotBootloaderProtocol.EndOfCommand],
                    options.Value.BootloaderSynchronizationTimeout,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is FirmwareBootloaderException or TimeoutException)
            {
                last = exception;
                if (attempt < options.Value.BootloaderSyncAttempts)
                    await Task.Delay(options.Value.BootloaderRetryDelay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new FirmwareBootloaderException("Unable to synchronize with the bootloader.", last);
    }

    private async Task<uint> GetInfoAsync(byte parameter, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await WriteAsync([ArduPilotBootloaderProtocol.GetDevice, parameter, ArduPilotBootloaderProtocol.EndOfCommand], timeout, cancellationToken).ConfigureAwait(false);
        var value = await ReadUInt32Async(timeout, cancellationToken).ConfigureAwait(false);
        await ReadStatusAsync(timeout, cancellationToken).ConfigureAwait(false);
        return value;
    }

    private async Task<string?> GetChipDescriptionAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await WriteAsync([ArduPilotBootloaderProtocol.GetChipDescription, ArduPilotBootloaderProtocol.EndOfCommand], timeout, cancellationToken).ConfigureAwait(false);
        var length = await ReadUInt32Async(timeout, cancellationToken).ConfigureAwait(false);
        if (length > 128) throw new FirmwareBootloaderException("Bootloader chip description is too long.");
        var data = new byte[length];
        await ReadExactAsync(data, timeout, cancellationToken).ConfigureAwait(false);
        await ReadStatusAsync(timeout, cancellationToken).ConfigureAwait(false);
        return length == 0 ? null : Encoding.ASCII.GetString(data);
    }

    private async Task<string?> TryGetChipDescriptionAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await GetChipDescriptionAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsOptionalIdentityFailure(exception))
        {
            logger.LogDebug(exception, "Bootloader did not provide an optional chip description; resynchronizing.");
            await SynchronizeAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private static bool IsOptionalIdentityFailure(Exception exception) =>
        exception is FirmwareBootloaderException or TimeoutException or IOException or EndOfStreamException or InvalidOperationException;

    private async Task ProgramImageAsync(ReadOnlyMemory<byte> image, byte command, long completedBefore, long total, IProgress<FirmwareProgress>? progress, CancellationToken cancellationToken)
    {
        var padded = Pad4(image.Span);
        for (var offset = 0; offset < padded.Length; offset += ArduPilotBootloaderProtocol.MaximumProgramChunk)
        {
            var length = Math.Min(ArduPilotBootloaderProtocol.MaximumProgramChunk, padded.Length - offset);
            var request = new byte[length + 3];
            request[0] = command;
            request[1] = checked((byte)length);
            padded.AsSpan(offset, length).CopyTo(request.AsSpan(2));
            request[^1] = ArduPilotBootloaderProtocol.EndOfCommand;
            await ExecuteStatusCommandAsync(request, options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
            var complete = completedBefore + Math.Min(offset + length, image.Length);
            progress?.Report(new FirmwareProgress(FirmwareOperationState.Programming, total == 0 ? 100 : complete * 100d / total, "program.progress", complete, total));
        }
    }

    private async Task<uint> GetChecksumAsync(byte command, CancellationToken cancellationToken)
    {
        await WriteAsync([command, ArduPilotBootloaderProtocol.EndOfCommand], options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
        var checksum = await ReadUInt32Async(options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
        await ReadStatusAsync(options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
        return checksum;
    }

    private async Task<uint> GetExternalChecksumAsync(int length, CancellationToken cancellationToken)
    {
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)length));
        await WriteAsync([ArduPilotBootloaderProtocol.ExternalGetCrc, .. size, ArduPilotBootloaderProtocol.EndOfCommand], options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
        var checksum = await ReadUInt32Async(options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
        await ReadStatusAsync(options.Value.BootloaderCommandTimeout, cancellationToken).ConfigureAwait(false);
        return checksum;
    }

    private async Task ExecuteStatusCommandAsync(byte[] command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await WriteAsync(command, timeout, cancellationToken).ConfigureAwait(false);
        await ReadStatusAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReadStatusAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var status = new byte[2];
        await ReadExactAsync(status, timeout, cancellationToken).ConfigureAwait(false);
        if (status[0] != ArduPilotBootloaderProtocol.InSync) throw new FirmwareBootloaderException($"Invalid sync byte 0x{status[0]:X2}.");
        if (status[1] != ArduPilotBootloaderProtocol.Ok)
            throw new FirmwareBootloaderException(status[1] switch { ArduPilotBootloaderProtocol.Failed => "Bootloader operation failed.", ArduPilotBootloaderProtocol.Invalid => "Bootloader rejected an invalid command.", ArduPilotBootloaderProtocol.BadSiliconRevision => "Bootloader rejected the silicon revision.", _ => $"Invalid status byte 0x{status[1]:X2}." });
    }

    private async Task<uint> ReadUInt32Async(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        await ReadExactAsync(buffer, timeout, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    private async Task ReadExactAsync(Memory<byte> destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var offset = 0;
        try
        {
            while (offset < destination.Length)
            {
                // SerialPort.BaseStream on Windows does not reliably observe the token passed to
                // ReadAsync. WaitAsync enforces our protocol deadline; discovery then disposes the
                // owning port, which releases the outstanding native read.
                var read = await port.Stream.ReadAsync(destination[offset..], CancellationToken.None)
                    .AsTask()
                    .WaitAsync(deadline.Token)
                    .ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Bootloader disconnected during a reply.");
                offset += read;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TimeoutException("Timed out waiting for a bootloader reply."); }
        catch (EndOfStreamException exception) { throw new FirmwareBootloaderException(exception.Message, exception); }
    }

    private async Task WriteAsync(byte[] data, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await port.Stream.WriteAsync(data, CancellationToken.None)
                .AsTask()
                .WaitAsync(deadline.Token)
                .ConfigureAwait(false);
            await port.Stream.FlushAsync(CancellationToken.None)
                .WaitAsync(deadline.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TimeoutException("Timed out writing a bootloader command."); }
    }

    private BootloaderIdentity RequireIdentity() => identity ?? throw new FirmwareBootloaderException("Bootloader identity must be read before destructive operations.");

    private static void ValidatePackage(BootloaderIdentity device, ApjFirmwarePackage package)
    {
        if (device.BoardId != package.BoardId && !(device.BoardId == 33 && package.BoardId == 9)) throw new FirmwareCompatibilityException($"Firmware board {package.BoardId} does not match bootloader board {device.BoardId}.");
        if (package.Image.Length > device.FlashSize) throw new FirmwareCompatibilityException("Firmware image exceeds application flash capacity.");
        if (package.ExternalImage.Length > device.ExternalFlashSize) throw new FirmwareCompatibilityException("External image exceeds external flash capacity.");
    }

    private static byte[] Pad4(ReadOnlySpan<byte> image)
    {
        var length = checked((image.Length + 3) & ~3);
        var result = new byte[length];
        result.AsSpan().Fill(0xff);
        image.CopyTo(result);
        return result;
    }
}
