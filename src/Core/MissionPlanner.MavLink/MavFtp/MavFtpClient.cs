using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.MavFtp;

/// <inheritdoc />
public sealed class MavFtpClient : IMavFtpClient
{
    private readonly IMavLinkConnection connection;
    private readonly IMavFtpMessageEncoder messageEncoder;
    private readonly IMavFtpPacketCodec packetCodec;
    private readonly IMavFtpResponseDispatcher responseDispatcher;
    private readonly ILogger<MavFtpClient> logger;
    private readonly MavFtpOptions options;
    private readonly ConcurrentDictionary<MavFtpTarget, TargetOperationState> targetStates = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MavFtpClient"/> class.
    /// </summary>
    /// <param name="connection">The MAVLink connection.</param>
    /// <param name="messageEncoder">The MAVLink FTP message encoder.</param>
    /// <param name="packetCodec">The MAVLink FTP packet codec.</param>
    /// <param name="responseDispatcher">The MAVLink FTP response dispatcher.</param>
    /// <param name="options">The MAVLink FTP options.</param>
    /// <param name="logger">The logger instance.</param>
    public MavFtpClient(IMavLinkConnection connection, IMavFtpMessageEncoder messageEncoder,
        IMavFtpPacketCodec packetCodec, IMavFtpResponseDispatcher responseDispatcher, IOptions<MavFtpOptions> options,
        ILogger<MavFtpClient> logger)
    {
        this.connection = connection;
        this.messageEncoder = messageEncoder;
        this.packetCodec = packetCodec;
        this.responseDispatcher = responseDispatcher;
        this.logger = logger;
        this.options = options.Value;
        this.options.Validate();
    }

    /// <inheritdoc />
    public Task ResetSessionsAsync(MavFtpTarget target, CancellationToken cancellationToken = default)
    {
        try
        {
            return InOperationLockAsync(target, async ct =>
            {
                await RequestAsync(target, 0, MavFtpOpcode.ResetSessions, 0, ReadOnlyMemory<byte>.Empty, null, ct).ConfigureAwait(false);
                return 0;
            }, cancellationToken);
        }
        catch (Exception)
        {
            //
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<MavFtpFileInfo> GetFileInfoAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            var opened = await OpenAsync(target, remotePath, ct).ConfigureAwait(false);
            try
            {
                return new MavFtpFileInfo(remotePath, opened.Size);
            }
            finally
            {
                await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MavFtpDirectoryEntry>> ListDirectoryAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            var entries = new List<MavFtpDirectoryEntry>();
            uint offset = 0;
            while (true)
            {
                MavFtpPacket response;
                try
                {
                    response = await RequestAsync(target, 0, MavFtpOpcode.ListDirectory, offset, PathBytes(remotePath), remotePath, ct).ConfigureAwait(false);
                }
                catch (MavFtpRemoteException ex)
                    when (ex.Error == MavFtpNakError.EndOfFile)
                {
                    break;
                }

                var page = MavFtpDirectoryCodec.Decode(response.Data.Span);
                if (page.Count == 0)
                {
                    break;
                }

                entries.AddRange(page.Where(x => x.Type != MavFtpDirectoryEntryType.Skip));
                offset += checked((uint)page.Count);
            }

            return (IReadOnlyList<MavFtpDirectoryEntry>)entries;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadFileAsync(MavFtpTarget target, string remotePath, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await DownloadFileAsync(target, remotePath, stream, progress, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    /// <inheritdoc />
    public Task DownloadFileAsync(MavFtpTarget target, string remotePath, Stream destination, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            if (!destination.CanWrite)
            {
                throw new ArgumentException("Destination stream must be writable.", nameof(destination));
            }

            var opened = await OpenAsync(target, remotePath, ct).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            long written = 0;
            try
            {
                var burstEnabled = options.PreferBurstRead;
                while (written < opened.Size)
                {
                    if (burstEnabled)
                    {
                        try
                        {
                            var length = Math.Min(options.BurstWindowSize, checked((int)(opened.Size - written)));
                            var window = await ReadBurstWindowAsync(target, opened.Session, checked((uint)written), length, remotePath, ct).ConfigureAwait(false);
                            await destination.WriteAsync(window, ct).ConfigureAwait(false);
                            written += window.Length;
                            progress?.Report(new MavFtpProgress(remotePath, written, opened.Size, written / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)));
                            continue;
                        }
                        catch (MavFtpRemoteException ex) when (ex.Error == MavFtpNakError.UnknownCommand)
                        {
                            logger.LogInformation("MAVFTP burst read is unsupported for {RemotePath}; falling back to regular reads.", remotePath);
                            burstEnabled = false;
                        }
                        catch (TimeoutException)
                        {
                            logger.LogInformation("MAVFTP burst read repeatedly timed out for {RemotePath}; falling back to regular reads.", remotePath);
                            burstEnabled = false;
                        }
                    }

                    var requested = Math.Min(options.ReadChunkSize, checked((int)(opened.Size - written)));
                    var response = await RequestAsync(target, opened.Session, MavFtpOpcode.ReadFile, checked((uint)written), new byte[requested], remotePath, ct).ConfigureAwait(false);
                    if (response.Offset != written || response.Data.IsEmpty)
                    {
                        throw new MavFtpProtocolException("MAVFTP returned an impossible or empty read block.");
                    }

                    await destination.WriteAsync(response.Data, ct).ConfigureAwait(false);
                    written += response.Data.Length;
                    progress?.Report(new MavFtpProgress(remotePath, written, opened.Size, written / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)));
                }

                progress?.Report(new MavFtpProgress(remotePath, written, opened.Size, written / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)));
            }
            finally
            {
                await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false);
            }

            return 0;
        }, cancellationToken);
    }

    private async Task<(byte Session, long Size)> OpenAsync(MavFtpTarget target, string path, CancellationToken ct)
    {
        var response = await RequestAsync(target, 0, MavFtpOpcode.OpenFileReadOnly, 0, PathBytes(path), path, ct).ConfigureAwait(false);
        return response.Data.Length != sizeof(uint)
            ? throw new MavFtpProtocolException("OpenFileRO ACK did not contain a 32-bit file size.")
            : ((byte Session, long Size))(response.Session, BinaryPrimitives.ReadUInt32LittleEndian(response.Data.Span));
    }

    private async Task<MavFtpPacket> RequestAsync(MavFtpTarget target, byte session, MavFtpOpcode opcode, uint offset, ReadOnlyMemory<byte> data, string? path, CancellationToken ct, TimeSpan? requestTimeout = null, int? maximumAttempts = null)
    {
        var state = targetStates.GetOrAdd(target, static _ => new TargetOperationState());
        var sequence = state.AllocateRequestSequence();
        var request = new MavFtpPacket(sequence, session, opcode, MavFtpOpcode.None, false, offset, data);
        for (var attempt = 1; ; attempt++)
        {
            using var registration = responseDispatcher.Register(target, sequence, opcode, opcode == MavFtpOpcode.OpenFileReadOnly ? null : session);
            await connection.SendRawAsync(messageEncoder.Encode(target.SystemId, target.ComponentId, packetCodec.Encode(request)), target.EndPoint, ct).ConfigureAwait(false);
            try
            {
                var response = await registration.ReadAsync(requestTimeout ?? options.RequestTimeout, ct).ConfigureAwait(false);
                state.ObserveResponse(response.Sequence);
                if (response.Opcode == MavFtpOpcode.Nak)
                {
                    if (response.Data.IsEmpty)
                    {
                        throw new MavFtpProtocolException("MAVFTP NAK has no error code.");
                    }

                    throw new MavFtpRemoteException((MavFtpNakError)response.Data.Span[0], response, path);
                }

                return response.Opcode != MavFtpOpcode.Ack ? throw new MavFtpProtocolException("Expected MAVFTP ACK or NAK.") : response;
            }
            catch (TimeoutException)
                when (attempt < (maximumAttempts ?? options.MaximumRequestAttempts))
            {
                logger.LogWarning("MAVFTP {Opcode} timed out; retrying attempt {Attempt}/{MaximumAttempts}.", opcode, attempt + 1, maximumAttempts ?? options.MaximumRequestAttempts);
            }
        }
    }

    private async Task<byte[]> ReadBurstWindowAsync(MavFtpTarget target, byte session, uint offset, int expectedLength, string path, CancellationToken ct)
    {
        var state = targetStates.GetOrAdd(target, static _ => new TargetOperationState());
        var sequence = state.AllocateRequestSequence();
        var request = new MavFtpPacket(sequence, session, MavFtpOpcode.BurstReadFile, MavFtpOpcode.None, false, offset, new byte[Math.Min(options.ReadChunkSize, expectedLength)]);
        using var registration = responseDispatcher.Register(target, sequence, MavFtpOpcode.BurstReadFile, session, true);
        await connection.SendRawAsync(messageEncoder.Encode(target.SystemId, target.ComponentId, packetCodec.Encode(request)), target.EndPoint, ct).ConfigureAwait(false);

        var blocks = new SortedDictionary<uint, byte[]>();
        var end = checked(offset + (uint)expectedLength);
        while (true)
        {
            MavFtpPacket packet;
            try
            {
                packet = await registration.ReadAsync(options.RequestTimeout, ct).ConfigureAwait(false);
                state.ObserveResponse(packet.Sequence);
            }
            catch (TimeoutException)
            {
                break;
            }

            if (packet.Opcode == MavFtpOpcode.Nak)
            {
                if (packet.Data.IsEmpty)
                {
                    throw new MavFtpProtocolException("MAVFTP burst NAK has no error code.");
                }

                var error = (MavFtpNakError)packet.Data.Span[0];
                if (error == MavFtpNakError.EndOfFile)
                {
                    break;
                }

                throw new MavFtpRemoteException(error, packet, path);
            }

            if (packet.Opcode != MavFtpOpcode.Ack || packet.Data.IsEmpty)
            {
                throw new MavFtpProtocolException("MAVFTP burst returned an invalid data packet.");
            }

            var blockEnd = checked(packet.Offset + (uint)packet.Data.Length);
            if (packet.Offset < offset || blockEnd > end)
            {
                throw new MavFtpProtocolException("MAVFTP burst block is outside the requested window.");
            }

            if (blocks.TryGetValue(packet.Offset, out var duplicate))
            {
                if (!duplicate.AsSpan().SequenceEqual(packet.Data.Span))
                {
                    throw new MavFtpProtocolException("Conflicting duplicate MAVFTP burst block.");
                }
            }
            else
            {
                foreach (var existing in blocks)
                {
                    var existingEnd = existing.Key + (uint)existing.Value.Length;
                    if (packet.Offset < existingEnd && blockEnd > existing.Key)
                    {
                        throw new MavFtpProtocolException("Overlapping MAVFTP burst blocks.");
                    }
                }

                blocks.Add(packet.Offset, packet.Data.ToArray());
            }

            if (packet.BurstComplete || CoveredLength(blocks) == expectedLength)
            {
                break;
            }
        }

        var result = new byte[expectedLength];
        var cursor = offset;
        foreach (var block in blocks)
        {
            if (block.Key > cursor)
            {
                await RecoverRangeAsync(target, session, cursor, checked((int)(block.Key - cursor)), result, offset, path, ct).ConfigureAwait(false);
            }

            block.Value.CopyTo(result, checked((int)(block.Key - offset)));
            cursor = Math.Max(cursor, block.Key + (uint)block.Value.Length);
        }

        if (cursor < end)
        {
            await RecoverRangeAsync(target, session, cursor, checked((int)(end - cursor)), result, offset, path, ct).ConfigureAwait(false);
        }

        return result;
    }

    private async Task RecoverRangeAsync(MavFtpTarget target, byte session, uint start, int length, byte[] result, uint windowOffset, string path, CancellationToken ct)
    {
        var remaining = length;
        var cursor = start;
        while (remaining > 0)
        {
            var count = Math.Min(options.ReadChunkSize, remaining);
            var packet = await RequestAsync(target, session, MavFtpOpcode.ReadFile, cursor, new byte[count], path, ct).ConfigureAwait(false);
            if (packet.Offset != cursor || packet.Data.IsEmpty || packet.Data.Length > count)
            {
                throw new MavFtpProtocolException("MAVFTP gap recovery returned an invalid block.");
            }

            packet.Data.CopyTo(result.AsMemory(checked((int)(cursor - windowOffset))));
            cursor += (uint)packet.Data.Length;
            remaining -= packet.Data.Length;
        }
    }

    private static int CoveredLength(SortedDictionary<uint, byte[]> blocks)
    {
        return blocks.Sum(x => x.Value.Length);
    }

    private async Task CleanupSessionAsync(MavFtpTarget target, byte session)
    {
        using var cleanupCancellation = new CancellationTokenSource(options.CleanupTimeout);
        try
        {
            await RequestAsync(
                target, session, MavFtpOpcode.TerminateSession, 0, ReadOnlyMemory<byte>.Empty, null,
                cleanupCancellation.Token, options.CleanupTimeout, options.CleanupAttempts).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not StackOverflowException and not OutOfMemoryException)
        {
            logger.LogWarning(ex, "MAVFTP session {Session} cleanup failed.", session);
        }
    }

    private async Task<T> InOperationLockAsync<T>(MavFtpTarget target, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var state = targetStates.GetOrAdd(target, static _ => new TargetOperationState());
        await state.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await action(ct).ConfigureAwait(false);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private static byte[] PathBytes(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Remote path cannot contain NUL.", nameof(path));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(path);
        return bytes.Length > 239
            ? throw new ArgumentOutOfRangeException(nameof(path), "UTF-8 remote path exceeds MAVFTP payload capacity.")
            : bytes;
    }

    private sealed class TargetOperationState
    {
        private int nextRequestSequence;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public ushort AllocateRequestSequence()
        {
            return unchecked((ushort)Volatile.Read(ref nextRequestSequence));
        }

        public void ObserveResponse(ushort responseSequence)
        {
            Volatile.Write(ref nextRequestSequence, MavFtpSequence.Next(responseSequence));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        responseDispatcher.Dispose();
        return connection.DisposeAsync();
    }
}
