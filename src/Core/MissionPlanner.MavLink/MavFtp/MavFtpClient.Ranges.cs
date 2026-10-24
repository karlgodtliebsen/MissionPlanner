using System.Buffers.Binary;

namespace MissionPlanner.MavLink.MavFtp;

public sealed partial class MavFtpClient
{
    /// <inheritdoc />
    public Task<(uint Size, byte[] Data)> ReadRangeAsync(MavFtpTarget target, string remotePath, uint offset, int maximumBytes, CancellationToken cancellationToken = default)
    {
        if (maximumBytes is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        return InOperationLockAsync(target, async token =>
        {
            var opened = await OpenAsync(target, remotePath, token).ConfigureAwait(false);
            try
            {
                if (opened.Size < 0 || opened.Size > uint.MaxValue) throw new MavFtpProtocolException("Invalid remote file size.");
                var size = (uint)opened.Size;
                if (offset >= size) return (size, Array.Empty<byte>());
                var remaining = (int)Math.Min(maximumBytes, size - offset);
                using var result = new MemoryStream(remaining);
                while (remaining > 0)
                {
                    var requested = Math.Min(remaining, options.ReadChunkSize);
                    var reply = await RequestAsync(target, opened.Session, MavFtpOpcode.ReadFile, offset + (uint)result.Length, new byte[requested], remotePath, token).ConfigureAwait(false);
                    if (reply.Offset != offset + result.Length || reply.Data.Length is 0 || reply.Data.Length > requested)
                        throw new MavFtpProtocolException("Invalid range read offset or payload size.");
                    result.Write(reply.Data.Span);
                    remaining -= reply.Data.Length;
                }
                return (size, result.ToArray());
            }
            finally { await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false); }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task AppendAsync(MavFtpTarget target, string remotePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (data.Length is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(data));
        return InOperationLockAsync(target, async token =>
        {
            // Confirm existence first. OpenFileWriteOnly must never be replaced by truncating CreateFile.
            var existing = await OpenAsync(target, remotePath, token).ConfigureAwait(false);
            await CleanupSessionAsync(target, existing.Session).ConfigureAwait(false);
            var opened = await RequestAsync(target, 0, MavFtpOpcode.OpenFileWriteOnly, 0, PathBytes(remotePath), remotePath, token).ConfigureAwait(false);
            try
            {
                if (opened.Data.Length != 4) throw new MavFtpProtocolException("Write-only open did not return the file size.");
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(opened.Data.Span);
                if (offset != existing.Size || (long)offset + data.Length > uint.MaxValue)
                    throw new IOException("The console file changed while opening it. Another writer may be active.");
                for (var written = 0; written < data.Length;)
                {
                    var count = Math.Min(239, data.Length - written);
                    var reply = await RequestAsync(target, opened.Session, MavFtpOpcode.WriteFile, offset + (uint)written, data.Slice(written, count), remotePath, token).ConfigureAwait(false);
                    if (reply.Offset != offset + written) throw new MavFtpProtocolException("Append acknowledgement has the wrong offset.");
                    written += count;
                }
                return 0;
            }
            finally { await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false); }
        }, cancellationToken);
    }
}
