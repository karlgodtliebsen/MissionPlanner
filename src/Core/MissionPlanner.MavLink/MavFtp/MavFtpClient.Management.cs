using System.Buffers.Binary;
using System.Diagnostics;

namespace MissionPlanner.MavLink.MavFtp;

public sealed partial class MavFtpClient
{
    /// <inheritdoc />
    public Task UploadFileAsync(MavFtpTarget target, string remotePath, Stream source, bool overwrite = false,
        IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("Source must be readable.", nameof(source));
        }
        if (source.CanSeek && source.Length - source.Position > uint.MaxValue)
        {
            throw new IOException("MAVFTP cannot address files larger than 4 GiB.");
        }
        var path = PathBytes(remotePath);
        return InOperationLockAsync(target, async ct =>
        {
            if (!overwrite)
            {
                await RequireMissingAsync(target, remotePath, ct).ConfigureAwait(false);
            }
            var opened = await RequestAsync(target, 0, MavFtpOpcode.CreateFile, 0, path, remotePath, ct).ConfigureAwait(false);
            var watch = Stopwatch.StartNew();
            long written = 0;
            uint checksum = 0;
            var buffer = new byte[239];
            long? total = source.CanSeek ? source.Length - source.Position : null;
            try
            {
                while (true)
                {
                    var count = await source.ReadAsync(buffer, ct).ConfigureAwait(false);
                    if (count == 0)
                    {
                        break;
                    }
                    if (written + count > uint.MaxValue)
                    {
                        throw new IOException("MAVFTP cannot address files larger than 4 GiB.");
                    }
                    var response = await RequestAsync(target, opened.Session, MavFtpOpcode.WriteFile,
                        (uint)written, buffer.AsMemory(0, count), remotePath, ct).ConfigureAwait(false);
                    if (response.Offset != written)
                    {
                        throw new MavFtpProtocolException("Write acknowledgement has the wrong offset.");
                    }
                    checksum = AccumulateCrc32(checksum, buffer.AsSpan(0, count));
                    written += count;
                    progress?.Report(new(remotePath, written, total, written / Math.Max(watch.Elapsed.TotalSeconds, 0.001)));
                }
                progress?.Report(new(remotePath, written, written, written / Math.Max(watch.Elapsed.TotalSeconds, 0.001)));
            }
            finally
            {
                await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false);
            }
            // Close first: the server must flush the file before calculating its checksum.
            var verified = await RequestAsync(target, 0, MavFtpOpcode.CalculateFileCrc32, 0, path, remotePath, ct).ConfigureAwait(false);
            if (verified.Data.Length != sizeof(uint) || BinaryPrimitives.ReadUInt32LittleEndian(verified.Data.Span) != checksum)
            {
                throw new MavFtpProtocolException("Uploaded file CRC does not match the source. The remote file remains for inspection; do not use it.");
            }
            return written;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(MavFtpTarget target, string remotePath, bool directory, CancellationToken cancellationToken = default)
    {
        return PathOperationAsync(target, directory ? MavFtpOpcode.RemoveDirectory : MavFtpOpcode.RemoveFile, remotePath, cancellationToken);
    }

    /// <inheritdoc />
    public Task CreateDirectoryAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default)
    {
        return PathOperationAsync(target, MavFtpOpcode.CreateDirectory, remotePath, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenameAsync(MavFtpTarget target, string remotePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var from = PathBytes(remotePath);
        var to = PathBytes(destinationPath);
        if (from.Length + to.Length + 2 > 239)
        {
            throw new ArgumentException("Both UTF-8 paths and separators must fit the MAVFTP payload.");
        }
        var data = new byte[from.Length + to.Length + 2];
        from.CopyTo(data, 0);
        to.CopyTo(data, from.Length + 1);
        return InOperationLockAsync(target, async ct =>
        {
            await RequireMissingAsync(target, destinationPath, ct).ConfigureAwait(false);
            return await RequestAsync(target, 0, MavFtpOpcode.Rename, 0, data, remotePath, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<uint> CalculateCrc32Async(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            var response = await RequestAsync(target, 0, MavFtpOpcode.CalculateFileCrc32, 0, PathBytes(remotePath), remotePath, ct).ConfigureAwait(false);
            if (response.Data.Length != sizeof(uint))
            {
                throw new MavFtpProtocolException("CRC acknowledgement must contain four bytes.");
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(response.Data.Span);
        }, cancellationToken);
    }

    private static uint AccumulateCrc32(uint checksum, ReadOnlySpan<byte> bytes)
    {
        // MAVFTP uses the reflected IEEE polynomial, seed zero and no final XOR.
        foreach (var value in bytes)
        {
            checksum ^= value;
            for (var bit = 0; bit < 8; bit++)
                checksum = (checksum >> 1) ^ ((checksum & 1) != 0 ? 0xEDB88320u : 0u);
        }
        return checksum;
    }

    private Task PathOperationAsync(MavFtpTarget target, MavFtpOpcode opcode, string path, CancellationToken cancellationToken)
    {
        var data = PathBytes(path);
        return InOperationLockAsync(target, ct => RequestAsync(target, 0, opcode, 0, data, path, ct), cancellationToken);
    }

    private async Task RequireMissingAsync(MavFtpTarget target, string path, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await OpenAsync(target, path, cancellationToken).ConfigureAwait(false);
            await CleanupSessionAsync(target, existing.Session).ConfigureAwait(false);
        }
        catch (MavFtpRemoteException exception) when (exception.Error == MavFtpNakError.FileNotFound)
        {
            return;
        }
        throw new IOException("The remote destination already exists. Choose a different name or remove it explicitly first.");
    }
}
